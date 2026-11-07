import json
import statistics
import time
import urllib.error
import urllib.parse
import urllib.request

from comun import BASE, arrancar, detener, sql


def pedir(metodo, ruta, cuerpo=None, token=None):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(BASE + ruta, data=datos, method=metodo)
    if datos is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    inicio = time.perf_counter()
    try:
        r = urllib.request.urlopen(req, timeout=120)
    except urllib.error.HTTPError as e:
        r = e
    bruto = r.read()
    t = time.perf_counter() - inicio
    codigo = r.code if hasattr(r, "code") else r.status
    return codigo, (json.loads(bruto.decode()) if bruto else None), t


api = arrancar()

print("volumen:", sql("SELECT (SELECT COUNT(*) FROM dbo.Juego) juegos,(SELECT COUNT(*) FROM dbo.Usuario) usuarios,(SELECT COUNT(*) FROM dbo.Calificacion) notas,(SELECT COUNT(*) FROM dbo.Resena) resenas,(SELECT COUNT(*) FROM dbo.BibliotecaUsuario) biblioteca,(SELECT COUNT(*) FROM dbo.ReporteResena) reportes"))

s, c, _ = pedir("POST", "/auth/registro", {"email": "medicion@prueba.test", "nombreUsuario": "medicion_p", "password": "Clave1234", "aceptaPrivacidad": True})
token = c["token"]
uid = c["usuario"]["usuarioId"]
sql(f"EXEC dbo.sp_AsignarRol @SuperAdminId = 1, @UsuarioId = {uid}, @RolId = 3")

juego_top = sql("SELECT TOP 1 JuegoId FROM dbo.Resena GROUP BY JuegoId ORDER BY COUNT(*) DESC")
usuario_top = sql("SELECT TOP 1 c.UsuarioId FROM dbo.Calificacion c JOIN dbo.Usuario u ON u.UsuarioId = c.UsuarioId WHERE u.Bloqueado = 0 GROUP BY c.UsuarioId ORDER BY COUNT(*) DESC")
g = urllib.parse.quote("accion,indie")
print("juego con mas resenas:", juego_top, "usuario con mas notas:", usuario_top)

CASOS = [
    ("GET", "/juegos", None),
    ("GET", "/juegos?orden=puntuacion", None),
    ("GET", "/juegos?orden=lanzamiento", None),
    ("GET", "/juegos?texto=a", None),
    ("GET", "/juegos?texto=witcher", None),
    ("GET", f"/juegos?generos={g}", None),
    ("GET", f"/juegos?texto=a&generos={g}&plataformas=windows&puntuacionMin=3&orden=puntuacion", None),
    ("GET", "/juegos?pagina=1500", None),
    ("GET", f"/juegos/{juego_top}", None),
    ("GET", f"/juegos/{juego_top}/resenas", None),
    ("GET", f"/juegos/{juego_top}/mi-estado", None),
    ("PUT", f"/juegos/{juego_top}/calificacion", {"puntuacion": 4}),
    ("PUT", f"/juegos/{juego_top}/biblioteca", {"estadoListaId": 1}),
    ("GET", f"/usuarios/{usuario_top}", None),
    ("GET", f"/usuarios/{usuario_top}/biblioteca", None),
    ("GET", "/generos", None),
    ("GET", "/moderacion/resenas", None),
    ("GET", "/moderacion/resenas?filtro=recientes", None),
    ("GET", "/moderacion/resenas?filtro=ocultas", None),
    ("GET", "/moderacion/usuarios-bloqueados", None),
    ("GET", "/admin/usuarios", None),
    ("GET", "/admin/usuarios?texto=usuario", None),
    ("GET", "/admin/auditoria", None),
]

peor = 0
for m, r, b in CASOS:
    tiempos, codigo = [], None
    for _ in range(4):
        codigo, _, t = pedir(m, r, b, token)
        tiempos.append(t)
    t = statistics.median(tiempos[1:])
    peor = max(peor, max(tiempos[1:]))
    marca = "  LENTO" if max(tiempos[1:]) >= 2 else ""
    print(f"{codigo} {t*1000:8.0f} ms  max {max(tiempos[1:])*1000:8.0f} ms  {m} {r[:70]}{marca}")

print(f"peor: {peor*1000:.0f} ms")
detener(api)
