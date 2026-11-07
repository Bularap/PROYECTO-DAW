import json
import urllib.error
import urllib.request

from comun import BASE, arrancar, caso, detener, resetear, sql, terminar


def pedir(metodo, ruta, cuerpo=None, token=None, cabeceras=None):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(BASE + ruta, data=datos, method=metodo)
    if datos is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    for k, v in (cabeceras or {}).items():
        req.add_header(k, v)
    try:
        r = urllib.request.urlopen(req)
    except urllib.error.HTTPError as e:
        r = e
    texto = r.read().decode()
    codigo = r.code if hasattr(r, "code") else r.status
    return codigo, (json.loads(texto) if texto else None), r.headers


resetear()
api = arrancar()

_, a, _ = pedir("POST", "/auth/registro", {"email": "ana@prueba.test", "nombreUsuario": "ana_prueba", "password": "Clave1234", "aceptaPrivacidad": True})
_, b, _ = pedir("POST", "/auth/registro", {"email": "beto@prueba.test", "nombreUsuario": "beto_prueba", "password": "Clave1234", "aceptaPrivacidad": True})
ta, tb = a["token"], b["token"]
ida = a["usuario"]["usuarioId"]

print("=== CU07 calificar")
s, c, _ = pedir("PUT", "/juegos/1/calificacion", {"puntuacion": 5})
caso("sin sesion", 401, s)
s, c, _ = pedir("GET", "/juegos/1")
base_prom, base_cant = c["promedioCalificacion"], c["cantidadCalificaciones"]
caso("metricas del seed", (3.33, 3), (base_prom, base_cant))
s, c, _ = pedir("PUT", "/juegos/1/calificacion", {"puntuacion": 5}, ta)
caso("califica y recalcula", (200, 3.75, 4), (s, c["promedioCalificacion"], c["cantidadCalificaciones"]))
s, c, _ = pedir("PUT", "/juegos/1/calificacion", {"puntuacion": 1}, ta)
caso("cambia sin duplicar", (200, 2.75, 4), (s, c["promedioCalificacion"], c["cantidadCalificaciones"]))
for nombre, cuerpo in [("puntuacion 0", {"puntuacion": 0}), ("puntuacion 6", {"puntuacion": 6}), ("puntuacion 300", {"puntuacion": 300}),
                       ("sin puntuacion", {}), ("puntuacion texto", {"puntuacion": "x"}), ("puntuacion decimal", {"puntuacion": 3.5})]:
    s, c, _ = pedir("PUT", "/juegos/1/calificacion", cuerpo, ta)
    caso(nombre, 400, s, c)
s, c, _ = pedir("PUT", "/juegos/99999/calificacion", {"puntuacion": 3}, ta)
caso("juego inexistente", 404, s, c)
s, c, _ = pedir("GET", "/juegos/1/mi-estado", token=ta)
caso("mi-estado refleja la nota", (200, 1), (s, c["puntuacion"]))
s, c, _ = pedir("DELETE", "/juegos/1/calificacion", token=ta)
caso("retira la nota", (200, 3.33, 3), (s, c["promedioCalificacion"], c["cantidadCalificaciones"]))
s, c, _ = pedir("DELETE", "/juegos/1/calificacion", token=ta)
caso("retirar de nuevo no falla", (200, 3), (s, c["cantidadCalificaciones"]))
s, c, _ = pedir("DELETE", "/juegos/99999/calificacion", token=ta)
caso("retirar de juego inexistente", 404, s, c)
sql("DELETE FROM dbo.Calificacion WHERE JuegoId = 3")
s, c, _ = pedir("GET", "/juegos/3")
caso("juego sin notas", (None, 0), (c["promedioCalificacion"], c["cantidadCalificaciones"]))

print("=== CU08 redactar")
TEXTO = "Muy buen juego, lo recomiendo con ganas."
s, c, _ = pedir("POST", "/juegos/1/resenas", {"contenido": TEXTO})
caso("sin sesion", 401, s)
pedir("PUT", "/juegos/1/calificacion", {"puntuacion": 4}, ta)
s, r1, _ = pedir("POST", "/juegos/1/resenas", {"contenido": TEXTO}, ta)
caso("publica", (201, TEXTO, "ana_prueba", 4, False, 1), (s, r1["contenido"], r1["nombreUsuario"], r1["puntuacion"], r1["editada"], r1["juegoId"]))
caso("  fecha en UTC", True, r1["fechaPublicacion"].endswith("Z"), r1["fechaPublicacion"])
s, c, _ = pedir("POST", "/juegos/1/resenas", {"contenido": TEXTO}, ta)
caso("duplicada", (409, "Ya publicaste una reseña para este juego."), (s, c["mensaje"]))
s, c, _ = pedir("POST", "/juegos/99999/resenas", {"contenido": TEXTO}, ta)
caso("juego inexistente", 404, s, c)
for nombre, contenido in [("vacia", ""), ("corta", "corta"), ("solo espacios", " " * 20), ("nula", None), ("larga", "x" * 2001), ("solo etiquetas", "<b><i><u><p></p></u></i></b>")]:
    s, c, _ = pedir("POST", "/juegos/2/resenas", {"contenido": contenido}, ta)
    caso(nombre, 400, s, c["mensaje"])
s, c, _ = pedir("POST", "/juegos/2/resenas", {"contenido": "x" * 2000}, ta)
caso("largo maximo", 201, s)
pedir("DELETE", f"/resenas/{c['resenaId']}", token=ta)
for nombre, entrada, esperado in [
    ("quita script", "<script>alert(1)</script>Un juego excelente de verdad", "alert(1)Un juego excelente de verdad"),
    ("quita anidado", "<<script>script>alert(1)<</script>/script> Un juego excelente", "alert(1) Un juego excelente"),
    ("quita sin cierre", "Un juego excelente <img src=x onerror=alert(1)", "Un juego excelente"),
    ("quita atributos", 'Un juego <a href="javascript:x" onclick="y">excelente</a> sin dudas', "Un juego excelente sin dudas"),
    ("conserva menor que", "Vale mas: 5 < 6 y eso me gusta", "Vale mas: 5 < 6 y eso me gusta"),
    ("conserva acentos y saltos", "Reseña con acentós\ny dos líneas", "Reseña con acentós\ny dos líneas"),
    ("quita controles", "Un juego\x00 excelente\x07 de verdad", "Un juego excelente de verdad"),
    ("SQL como texto", "'; DROP TABLE dbo.Resena;-- es muy bueno", "'; DROP TABLE dbo.Resena;-- es muy bueno"),
]:
    s, c, _ = pedir("POST", "/juegos/2/resenas", {"contenido": entrada}, ta)
    caso(nombre, (201, esperado), (s, c.get("contenido")))
    if s == 201:
        pedir("DELETE", f"/resenas/{c['resenaId']}", token=ta)

print("=== CU12 feed")
s, c, _ = pedir("GET", "/juegos/1/resenas")
caso("visitante puede leer", 200, s)
caso("  mas reciente primero", "ana_prueba", c["resenas"][0]["nombreUsuario"])
caso("  excluye autor bloqueado", (2, False), (c["total"], "spammer99" in [x["nombreUsuario"] for x in c["resenas"]]))
s, c, _ = pedir("GET", "/juegos/1/resenas?tamanioPagina=1&pagina=2")
caso("  paginacion", (200, 1, 2, "lucia_rpg"), (s, len(c["resenas"]), c["total"], c["resenas"][0]["nombreUsuario"]))
s, c, _ = pedir("GET", "/juegos/3/resenas")
caso("  sin resenas", (200, 0, []), (s, c["total"], c["resenas"]))
for nombre, ruta in [("pagina invalida", "/juegos/1/resenas?pagina=0"), ("tamanio invalido", "/juegos/1/resenas?tamanioPagina=51"), ("tamanio texto", "/juegos/1/resenas?tamanioPagina=x")]:
    s, c, _ = pedir("GET", ruta)
    caso("  " + nombre, 400, s, c)

print("=== CU09 editar y eliminar")
rid = r1["resenaId"]
NUEVO = "Texto corregido despues de jugar mas."
s, c, _ = pedir("PUT", f"/resenas/{rid}", {"contenido": NUEVO})
caso("sin sesion", 401, s)
s, c, _ = pedir("PUT", f"/resenas/{rid}", {"contenido": NUEVO}, tb)
caso("otro usuario no edita", (403, "Solo el autor puede modificar la reseña."), (s, c["mensaje"]))
s, c, _ = pedir("PUT", f"/resenas/{rid}", {"contenido": "corta"}, ta)
caso("contenido invalido", 400, s)
s, c, _ = pedir("PUT", f"/resenas/{rid}", {"contenido": NUEVO}, ta)
caso("autor edita", (200, NUEVO, True), (s, c["contenido"], c["editada"]))
s, c, _ = pedir("PUT", "/resenas/99999", {"contenido": NUEVO}, ta)
caso("inexistente", (404, "La reseña no existe."), (s, c["mensaje"]))
s, c, _ = pedir("GET", "/juegos/1/mi-estado", token=ta)
caso("mi-estado trae la resena para editar", (rid, NUEVO), (c["resenaId"], c["resenaContenido"]))

sql(f"UPDATE dbo.Resena SET Estado = 'O' WHERE ResenaId = {rid}")
s, c, _ = pedir("PUT", f"/resenas/{rid}", {"contenido": NUEVO}, ta)
caso("oculta no se edita", 404, s, c)
s, c, _ = pedir("DELETE", f"/resenas/{rid}", token=ta)
caso("oculta no se elimina", 404, s, c)
s, c, _ = pedir("POST", f"/resenas/{rid}/reportes", {"motivo": "SPAM"}, tb)
caso("oculta no se reporta", 404, s, c)
s, c, _ = pedir("GET", "/juegos/1/resenas")
caso("oculta no aparece en el feed", False, rid in [x["resenaId"] for x in c["resenas"]])
s, c, _ = pedir("GET", "/juegos/1/mi-estado", token=ta)
caso("oculta no aparece en mi-estado", None, c["resenaId"])
s, c, _ = pedir("GET", f"/usuarios/{ida}", token=ta)
caso("oculta no aparece en el perfil", (0, []), (c["cantidadResenas"], c["resenas"]))
sql(f"UPDATE dbo.Resena SET Estado = 'V' WHERE ResenaId = {rid}")

print("=== Reportes")
s, c, _ = pedir("POST", f"/resenas/{rid}/reportes", {"motivo": "SPAM"})
caso("sin sesion", 401, s)
s, c, _ = pedir("POST", f"/resenas/{rid}/reportes", {"motivo": "spam", "comentario": "<b>publicidad</b>"}, tb)
caso("reporta", 204, s)
caso("  comentario limpio", "SPAM publicidad", sql(f"SELECT Motivo, Comentario FROM dbo.ReporteResena WHERE ResenaId = {rid}"))
s, c, _ = pedir("POST", f"/resenas/{rid}/reportes", {"motivo": "SPAM"}, tb)
caso("reporte duplicado", 409, s, c)
for nombre, cuerpo in [("motivo invalido", {"motivo": "OTRA COSA"}), ("sin motivo", {}), ("comentario largo", {"motivo": "OTRO", "comentario": "x" * 301})]:
    s, c, _ = pedir("POST", f"/resenas/{rid}/reportes", cuerpo, ta)
    caso(nombre, 400, s, c)
s, c, _ = pedir("POST", "/resenas/99999/reportes", {"motivo": "SPAM"}, tb)
caso("resena inexistente", 404, s, c)

s, c, _ = pedir("DELETE", f"/resenas/{rid}", token=tb)
caso("otro usuario no elimina", 403, s, c)
s, c, _ = pedir("DELETE", f"/resenas/{rid}", token=ta)
caso("autor elimina", 204, s)
s, c, _ = pedir("DELETE", f"/resenas/{rid}", token=ta)
caso("eliminar de nuevo", 404, s, c)
s, c, _ = pedir("POST", "/juegos/1/resenas", {"contenido": TEXTO}, ta)
caso("puede volver a publicar", 201, s)

print("=== CU10 listas")
s, c, _ = pedir("GET", "/estados-lista")
caso("estados para visitantes", (200, ["Jugado", "Quiero jugarlo", "Abandonado"]), (s, [e["nombre"] for e in c]))
s, c, _ = pedir("PUT", "/juegos/2/biblioteca", {"estadoListaId": 1})
caso("sin sesion", 401, s)
s, c, _ = pedir("PUT", "/juegos/2/biblioteca", {"estadoListaId": 1}, ta)
caso("agrega", 204, s)
s, c, _ = pedir("PUT", "/juegos/2/biblioteca", {"estadoListaId": 3}, ta)
caso("cambia", 204, s)
caso("  un solo estado vigente", f"1 3", sql(f"SELECT COUNT(*), MAX(EstadoListaId) FROM dbo.BibliotecaUsuario WHERE UsuarioId = {ida}"))
s, c, _ = pedir("GET", "/juegos/2/mi-estado", token=ta)
caso("  mi-estado lo refleja", 3, c["estadoListaId"])
for nombre, cuerpo in [("estado inexistente", {"estadoListaId": 9}), ("estado fuera de rango", {"estadoListaId": 300}), ("estado cero", {"estadoListaId": 0}), ("sin estado", {})]:
    s, c, _ = pedir("PUT", "/juegos/2/biblioteca", cuerpo, ta)
    caso(nombre, 400, s, c)
s, c, _ = pedir("GET", "/juegos/2/mi-estado", token=ta)
caso("  el estado no cambio", 3, c["estadoListaId"])
s, c, _ = pedir("PUT", "/juegos/99999/biblioteca", {"estadoListaId": 1}, ta)
caso("juego inexistente", 404, s, c)
s, c, _ = pedir("DELETE", "/juegos/2/biblioteca", token=ta)
caso("quita", 204, s)
s, c, _ = pedir("DELETE", "/juegos/2/biblioteca", token=ta)
caso("quitar de nuevo no falla", 204, s)
s, c, _ = pedir("GET", "/juegos/99999/mi-estado", token=ta)
caso("mi-estado juego inexistente", 404, s, c)
s, c, _ = pedir("GET", "/juegos/2/mi-estado")
caso("mi-estado sin sesion", 401, s)

print("=== CU11 perfil")
s, c, _ = pedir("GET", "/usuarios/3")
caso("sin sesion", 401, s)
s, c, _ = pedir("GET", "/usuarios/3", token=ta)
caso("perfil de otro", (200, "lucia_rpg", 4.25, 4, 2), (s, c["nombreUsuario"], c["promedioOtorgado"], c["cantidadCalificaciones"], c["cantidadResenas"]))
caso("  cantidades por lista", [2, 1, 1], [x["cantidad"] for x in c["listas"]])
caso("  historial y resenas", (4, 2), (len(c["calificaciones"]), len(c["resenas"])))
caso("  no expone datos privados", False, any(k in json.dumps(c) for k in ["email", "gamerank.test", "passwordHash"]))
caso("  fechas en UTC", True, c["fechaRegistro"].endswith("Z") and c["calificaciones"][0]["fecha"].endswith("Z"))
s, c, _ = pedir("GET", f"/usuarios/{b['usuario']['usuarioId']}", token=tb)
caso("sin actividad", (200, None, 0, [], []), (s, c["promedioOtorgado"], c["cantidadCalificaciones"], c["calificaciones"], c["resenas"]))
s, c, _ = pedir("GET", "/usuarios/6", token=ta)
caso("bloqueado", (404, "Perfil no disponible."), (s, c["mensaje"]))
s, c, _ = pedir("GET", "/usuarios/99999", token=ta)
caso("inexistente", 404, s, c)
s, c, _ = pedir("GET", "/usuarios/3/biblioteca", token=ta)
caso("biblioteca completa", (200, 4), (s, len(c)))
caso("  datos de cada juego", True, all(x["titulo"] and x["estado"] for x in c))
s, c, _ = pedir("GET", "/usuarios/3/biblioteca?estadoListaId=1", token=ta)
caso("biblioteca filtrada", (200, 2, {"Jugado"}), (s, len(c), {x["estado"] for x in c}))
s, c, _ = pedir("GET", "/usuarios/3/biblioteca?estadoListaId=300", token=ta)
caso("filtro invalido", 400, s, c)
s, c, _ = pedir("GET", "/usuarios/6/biblioteca", token=ta)
caso("biblioteca de bloqueado", 404, s, c)
s, c, _ = pedir("GET", "/usuarios/3/biblioteca")
caso("biblioteca sin sesion", 401, s)

print("=== Otros")
s, c, h = pedir("OPTIONS", "/juegos/1/calificacion", cabeceras={"Origin": "http://127.0.0.1:5500", "Access-Control-Request-Method": "PUT", "Access-Control-Request-Headers": "authorization,content-type"})
caso("CORS permite PUT con token", (204, True), (s, "PUT" in (h.get("Access-Control-Allow-Methods") or "")))
s, c, _ = pedir("GET", "/auth/yo", token=ta)
caso("auth sigue funcionando", (200, ida), (s, c["usuarioId"]))
s, c, _ = pedir("GET", "/juegos?texto=witcher")
caso("catalogo sigue funcionando", (200, 1), (s, c["total"]))

detener(api)
resetear()
print("base final:", sql("SELECT (SELECT COUNT(*) FROM dbo.Usuario), (SELECT COUNT(*) FROM dbo.Resena), (SELECT COUNT(*) FROM dbo.SincronizacionApi), (SELECT COUNT(*) FROM sys.procedures)"))
terminar()
