import hashlib
import io
import json
import os
import shutil
import urllib.error
import urllib.request
import zipfile

from comun import BASE, TEMPORAL, arrancar, caso, detener, resetear, sql, terminar


def pedir(metodo, ruta, cuerpo=None, token=None, crudo=False):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(BASE + ruta, data=datos, method=metodo)
    if datos is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        r = urllib.request.urlopen(req, timeout=300)
    except urllib.error.HTTPError as e:
        r = e
    bruto = r.read()
    codigo = r.code if hasattr(r, "code") else r.status
    if crudo:
        return codigo, bruto
    return codigo, (json.loads(bruto.decode()) if bruto else None)


def registrar(nombre):
    s, c = pedir("POST", "/auth/registro", {"email": f"{nombre}@prueba.test", "nombreUsuario": nombre, "password": "Clave1234", "aceptaPrivacidad": True})
    return c["token"], c["usuario"]["usuarioId"]


def marcas():
    return sorted(sql("SELECT NombreUsuario FROM dbo.Usuario WHERE NombreUsuario LIKE 'marca%'").split())


def historial(token):
    return pedir("GET", "/admin/respaldos?tamanioPagina=100", token=token)[1]["items"]


DEFECTO = sql("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(400))", "master")
DIR = os.path.join(DEFECTO, "GameRank")
ajenos_antes = sorted((f, os.path.getsize(os.path.join(DEFECTO, f))) for f in os.listdir(DEFECTO) if os.path.isfile(os.path.join(DEFECTO, f)))
if os.path.isdir(DIR):
    raise SystemExit("ya existe la carpeta de respaldos; no se ejecuta la prueba")

resetear()
api = arrancar()
tj, _ = registrar("jugador_p")
ta, ida = registrar("admin_p")
ts, ids = registrar("super_p")
sql(f"EXEC dbo.sp_AsignarRol @SuperAdminId = 1, @UsuarioId = {ids}, @RolId = 3; EXEC dbo.sp_AsignarRol @SuperAdminId = 1, @UsuarioId = {ida}, @RolId = 2;")

print("=== Acceso y validaciones")
for m, r, b in [("GET", "/admin/respaldos", None), ("POST", "/admin/respaldos", {"tipo": "COMPLETO"}), ("GET", "/admin/respaldos/1/descarga", None), ("POST", "/admin/respaldos/1/restauracion", {"confirmacion": "RESTAURAR"})]:
    caso(f"{m} {r} sin sesion", 401, pedir(m, r, b)[0])
    caso(f"{m} {r} como jugador", 403, pedir(m, r, b, tj)[0])
    caso(f"{m} {r} como administrador", 403, pedir(m, r, b, ta)[0])
caso("sigue sin respaldos", "0", sql("SELECT COUNT(*) FROM dbo.Respaldo"))
s, c = pedir("POST", "/admin/respaldos", {"tipo": "OTRO"}, ts)
caso("tipo invalido", 400, s, c)
s, c = pedir("POST", "/admin/respaldos", {}, ts)
caso("sin tipo", 400, s, c)
s, c = pedir("POST", "/admin/respaldos", {"tipo": "DIFERENCIAL"}, ts)
caso("diferencial sin completo", (409, "Primero debe generarse un respaldo completo."), (s, c["mensaje"]))
s, c = pedir("POST", "/admin/respaldos", {"tipo": "LOG"}, ts)
caso("log sin completo", 409, s, c)
s, c = pedir("GET", "/admin/respaldos?pagina=0", token=ts)
caso("pagina invalida", 400, s, c)

print("=== Generar")
s, f = pedir("POST", "/admin/respaldos", {"tipo": "completo"}, ts)
caso("completo", (201, "COMPLETO", "BAK", "OK"), (s, f["tipo"], f["formato"], f["estado"]))
caso("  tamano y checksum", True, f["tamanioBytes"] > 0 and len(f["checksumSha256"]) == 64)
caso("  no expone la ruta interna", False, "rutaArchivo" in f or "Microsoft SQL" in json.dumps(f), f["nombreArchivo"])
caso("  archivo en la subcarpeta", True, os.path.isfile(os.path.join(DIR, f["nombreArchivo"])))
caso("  auditoria", f"GENERAR_RESPALDO {ids} {f['respaldoId']}", sql("SELECT TOP 1 Accion, UsuarioActorId, EntidadId FROM dbo.Auditoria ORDER BY AuditoriaId DESC"))
registrar("marca1")
s, d = pedir("POST", "/admin/respaldos", {"tipo": "DIFERENCIAL"}, ts)
caso("diferencial", (201, "BAK", "OK"), (s, d["formato"], d["estado"]))
registrar("marca2")
s, l = pedir("POST", "/admin/respaldos", {"tipo": "LOG"}, ts)
caso("log", (201, "TRN", "OK"), (s, l["formato"], l["estado"]))
registrar("marca3")
caso("marcas iniciales", ["marca1", "marca2", "marca3"], marcas())

print("=== Descarga y exportacion")
s, bruto = pedir("GET", f"/admin/respaldos/{f['respaldoId']}/descarga", token=ts, crudo=True)
caso("descarga igual al checksum", (200, f["checksumSha256"]), (s, hashlib.sha256(bruto).hexdigest()))
s, c = pedir("GET", "/admin/respaldos/99999/descarga", token=ts)
caso("descarga inexistente", 404, s, c)
tj2, _ = registrar("formula_p")
pedir("POST", "/juegos/3/resenas", {"contenido": '=SUM(A1) es "bueno", con coma\ny acentó'}, tj2)
s, e = pedir("POST", "/admin/respaldos", {"tipo": "EXPORTACION"}, ts)
caso("exportacion", (201, "CSV", "OK"), (s, e["formato"], e["estado"]))
s, bruto = pedir("GET", f"/admin/respaldos/{e['respaldoId']}/descarga", token=ts, crudo=True)
z = zipfile.ZipFile(io.BytesIO(bruto))
caso("  once tablas", 11, len(z.namelist()), z.namelist())
todo = "".join(z.read(n).decode("utf-8-sig") for n in z.namelist())
caso("  sin datos privados", False, any(x in todo for x in ["prueba.test", "gamerank.test", "$2a$", "$2b$", "SEED_SIN_LOGIN", "PasswordHash", "Email"]))
caso("  sin tablas sensibles", False, any(n.startswith(("Sesion", "Auditoria", "Respaldo")) for n in z.namelist()))
caso("  encabezado", "GeneroId,SteamId,Nombre,Slug", z.read("Genero.csv").decode("utf-8-sig").splitlines()[0])
caso("  filas completas", 7, len(z.read("Juego.csv").decode("utf-8-sig").replace("\r\n", "\n").strip().split("\n")) - 1)
caso("  formula neutralizada y comillas", True, "\"'=SUM(A1) es \"\"bueno\"\", con coma" in z.read("Resena.csv").decode("utf-8-sig"))
caso("  con BOM para Excel", True, z.read("Genero.csv")[:3] == b"\xef\xbb\xbf")

print("=== Restaurar: validaciones")
idf, idd, idl = f["respaldoId"], d["respaldoId"], l["respaldoId"]
for nombre, cuerpo in [("sin confirmacion", {}), ("confirmacion incorrecta", {"confirmacion": "si"})]:
    s, c = pedir("POST", f"/admin/respaldos/{idf}/restauracion", cuerpo, ts)
    caso(nombre, 400, s, c)
caso("  no cambio nada", 3, len(marcas()))
C = {"confirmacion": "RESTAURAR"}
s, c = pedir("POST", "/admin/respaldos/99999/restauracion", C, ts)
caso("inexistente", 404, s, c)
s, c = pedir("POST", f"/admin/respaldos/{e['respaldoId']}/restauracion", C, ts)
caso("exportacion no se restaura", 400, s, c)

print("=== Restaurar: cadena de log")
n_antes = len(historial(ts))
s, r = pedir("POST", f"/admin/respaldos/{idl}/restauracion", C, ts)
caso("restaura log", (200, idl), (s, r.get("respaldoRestauradoId")), r)
caso("  vuelve hasta el log", ["marca1", "marca2"], marcas())
h = historial(ts)
caso("  historial completo mas seguridad y nueva base", n_antes + 2, len(h))
caso("  ninguno quedo en curso", [], [x["respaldoId"] for x in h if x["estado"] == "EN_CURSO"])
caso("  objetivo marcado", "RESTAURADO", next(x["estado"] for x in h if x["respaldoId"] == idl))
caso("  el diferencial sigue OK", "OK", next(x["estado"] for x in h if x["respaldoId"] == idd))
caso("  auditoria", f"RESTAURAR_RESPALDO {idl}", sql("SELECT TOP 1 Accion, EntidadId FROM dbo.Auditoria WHERE Accion = 'RESTAURAR_RESPALDO' ORDER BY AuditoriaId DESC"))
caso("  base disponible", "MULTI_USER ONLINE", sql("SELECT user_access_desc, state_desc FROM sys.databases WHERE name = 'GameRank'", "master"))
caso("  la API sigue funcionando", 200, pedir("GET", "/juegos")[0])
caso("  el token sigue funcionando", 200, pedir("GET", "/auth/yo", token=ts)[0])

print("=== Restaurar: diferencial y completo")
s, r = pedir("POST", f"/admin/respaldos/{idd}/restauracion", C, ts)
caso("restaura diferencial", 200, s, r)
caso("  vuelve hasta el diferencial", ["marca1"], marcas())
s, r = pedir("POST", f"/admin/respaldos/{idf}/restauracion", C, ts)
caso("restaura completo", 200, s, r)
caso("  vuelve hasta el completo", [], marcas())
caso("  la seguridad recupera el estado previo", True, r["respaldoSeguridadId"] > 0)
seg = r["respaldoSeguridadId"]
s, r2 = pedir("POST", f"/admin/respaldos/{seg}/restauracion", C, ts)
caso("  deshacer con el respaldo de seguridad", 200, s, r2)

print("=== Diferencial despues de restaurar")
registrar("marca4")
s, d2 = pedir("POST", "/admin/respaldos", {"tipo": "DIFERENCIAL"}, ts)
caso("genera", 201, s)
registrar("marca5")
s, r = pedir("POST", f"/admin/respaldos/{d2['respaldoId']}/restauracion", C, ts)
caso("restaura con la nueva base", 200, s, r)
caso("  estado correcto", True, "marca4" in marcas() and "marca5" not in marcas(), marcas())

print("=== Responsable que no existe en el respaldo")
s, f2 = pedir("POST", "/admin/respaldos", {"tipo": "COMPLETO"}, ts)
tn, idn = registrar("super_nuevo")
pedir("PUT", f"/admin/usuarios/{idn}/rol", {"rolId": 3}, ts)
s, r = pedir("POST", f"/admin/respaldos/{f2['respaldoId']}/restauracion", C, tn)
caso("restaura igual", 200, s, r)
caso("  su cuenta ya no existe", 401, pedir("GET", "/auth/yo", token=tn)[0])
caso("  auditoria deja constancia", f"SolicitadoPor={idn}", sql("SELECT TOP 1 Detalle FROM dbo.Auditoria WHERE Accion = 'RESTAURAR_RESPALDO' ORDER BY AuditoriaId DESC"))
caso("  historial intacto", True, f2["respaldoId"] in [x["respaldoId"] for x in historial(ts)])

print("=== Archivo danado o faltante")
registrar("marca6")
s, f3 = pedir("POST", "/admin/respaldos", {"tipo": "COMPLETO"}, ts)
ruta = os.path.join(DIR, f3["nombreArchivo"])
copia = os.path.join(TEMPORAL, "copia.bak")
shutil.copy(ruta, copia)
with open(ruta, "r+b") as a:
    a.seek(4096)
    a.write(b"\x00" * 64)
registrar("marca7")
n_antes = len(historial(ts))
s, c = pedir("POST", f"/admin/respaldos/{f3['respaldoId']}/restauracion", C, ts)
caso("danado", 409, s, c["mensaje"])
caso("  conserva la base", True, "marca7" in marcas())
caso("  no genero respaldos", n_antes, len(historial(ts)))
os.remove(ruta)
s, c = pedir("POST", f"/admin/respaldos/{f3['respaldoId']}/restauracion", C, ts)
caso("faltante", 409, s, c["mensaje"])
s, c = pedir("GET", f"/admin/respaldos/{f3['respaldoId']}/descarga", token=ts)
caso("descarga faltante", 409, s, c["mensaje"])
shutil.copy(copia, ruta)
os.remove(copia)
s, c = pedir("POST", f"/admin/respaldos/{f3['respaldoId']}/restauracion", C, ts)
caso("restaurado el archivo original funciona", 200, s, c)

print("=== Falla durante la restauracion")
s, a1 = pedir("POST", "/admin/respaldos", {"tipo": "COMPLETO"}, ts)
s, a2 = pedir("POST", "/admin/respaldos", {"tipo": "COMPLETO"}, ts)
s, a3 = pedir("POST", "/admin/respaldos", {"tipo": "DIFERENCIAL"}, ts)
registrar("marca8")
sql(f"UPDATE dbo.Respaldo SET Estado = 'ERROR' WHERE RespaldoId = {a2['respaldoId']}")
s, c = pedir("POST", f"/admin/respaldos/{a3['respaldoId']}/restauracion", C, ts)
caso("diferencial con base incorrecta", 500, s, (c or {}).get("mensaje", "")[:70])
caso("  avisa que recupero el estado", True, (c or {}).get("mensaje", "").startswith("No se pudo restaurar el respaldo y se recuperó el estado anterior."))
caso("  recupera el estado anterior", True, "marca8" in marcas())
caso("  ninguno quedo en curso", [], [x["respaldoId"] for x in historial(ts) if x["estado"] == "EN_CURSO"])
caso("  el objetivo no se marco restaurado", "OK", next(x["estado"] for x in historial(ts) if x["respaldoId"] == a3["respaldoId"]))
caso("  base disponible", "MULTI_USER ONLINE", sql("SELECT user_access_desc, state_desc FROM sys.databases WHERE name = 'GameRank'", "master"))
caso("  la API sigue funcionando", 200, pedir("GET", "/juegos")[0])
caso("  el historial sigue", True, a3["respaldoId"] in [x["respaldoId"] for x in historial(ts)])
detener(api)

print("=== Carpeta sin permisos")
sin_permiso = os.path.join(TEMPORAL, "sin_permiso")
os.makedirs(sin_permiso, exist_ok=True)
api = arrancar(Respaldo__Directorio=sin_permiso)
s, c = pedir("POST", "/admin/respaldos", {"tipo": "COMPLETO"}, ts)
caso("informa el error", 500, s, (c or {}).get("mensaje", "")[:80])
caso("  sin archivos parciales", [], os.listdir(sin_permiso))
detener(api)
shutil.rmtree(sin_permiso)
api = arrancar(Respaldo__Directorio=r"Z:\noexiste\GameRank")
s, c = pedir("POST", "/admin/respaldos", {"tipo": "COMPLETO"}, ts)
caso("carpeta invalida", 500, s, (c or {}).get("mensaje", "")[:80])
s, c = pedir("POST", "/admin/respaldos", {"tipo": "EXPORTACION"}, ts)
caso("exportacion con carpeta invalida", 500, s)
detener(api)

print("=== BACKUP que falla en el motor")
resetear()
api = arrancar()
ts, ids = registrar("super_r")
sql(f"EXEC dbo.sp_AsignarRol @SuperAdminId = 1, @UsuarioId = {ids}, @RolId = 3")
sql(f"INSERT INTO dbo.Respaldo (UsuarioId, Tipo, Formato, RutaArchivo, Estado, FechaFin) VALUES ({ids}, 'COMPLETO', 'BAK', N'falso.bak', 'OK', SYSUTCDATETIME())")
archivos_antes = sorted(os.listdir(DIR))
s, c = pedir("POST", "/admin/respaldos", {"tipo": "DIFERENCIAL"}, ts)
caso("diferencial sin base real", 500, s, (c or {}).get("mensaje", "")[:80])
caso("  queda registrado como ERROR", "DIFERENCIAL ERROR", sql("SELECT TOP 1 Tipo, Estado FROM dbo.Respaldo ORDER BY RespaldoId DESC"))
caso("  guarda el motivo y la fecha de fin", "1", sql("SELECT TOP 1 CASE WHEN MensajeError IS NOT NULL AND FechaFin IS NOT NULL THEN 1 ELSE 0 END FROM dbo.Respaldo ORDER BY RespaldoId DESC"))
caso("  sin archivos parciales", archivos_antes, sorted(os.listdir(DIR)))
caso("  no audita como generado", "0", sql("SELECT COUNT(*) FROM dbo.Auditoria WHERE Accion = 'GENERAR_RESPALDO'"))
detener(api)

resetear()
shutil.rmtree(DIR)
ajenos_despues = sorted((f, os.path.getsize(os.path.join(DEFECTO, f))) for f in os.listdir(DEFECTO) if os.path.isfile(os.path.join(DEFECTO, f)))
caso("otros archivos de la carpeta intactos", ajenos_antes, ajenos_despues)
print("base final:", sql("SELECT (SELECT COUNT(*) FROM dbo.Juego), (SELECT COUNT(*) FROM dbo.Usuario), (SELECT COUNT(*) FROM dbo.Respaldo), (SELECT COUNT(*) FROM sys.procedures)"))
terminar()
