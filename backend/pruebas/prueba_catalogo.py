import http.server
import json
import os
import threading
import time
import urllib.error
import urllib.parse
import urllib.request

from comun import BASE, RAIZ, arrancar, caso, detener, resetear, sql, terminar


def pedir(ruta):
    req = urllib.request.Request(BASE + ruta)
    inicio = time.time()
    try:
        r = urllib.request.urlopen(req)
    except urllib.error.HTTPError as e:
        r = e
    texto = r.read().decode()
    return r.code if hasattr(r, "code") else r.status, (json.loads(texto) if texto else None), time.time() - inicio


class Api:
    def __init__(self, **entorno):
        self.p = arrancar(**entorno)

    def cerrar(self):
        detener(self.p)


def recrear():
    resetear()
    sql("DELETE FROM dbo.SincronizacionApi")


def esperar_bitacora(origen, cantidad=1):
    for _ in range(60):
        if sql(f"SELECT COUNT(*) FROM dbo.SincronizacionApi WHERE Origen='{origen}' AND Estado <> 'EN_CURSO'") == str(cantidad):
            return True
        time.sleep(0.25)
    return False


def bitacora():
    return sql("SELECT TOP 1 Origen, Estado, RegistrosInsertados, RegistrosActualizados, RegistrosConError, Reintentos FROM dbo.SincronizacionApi ORDER BY SincronizacionId DESC")


def ultimo_mensaje():
    return sql("SELECT TOP 1 MensajeError FROM dbo.SincronizacionApi ORDER BY SincronizacionId DESC")


def titulos(c):
    return [j["titulo"] for j in c["juegos"]]


def id_por_titulo(titulo):
    return pedir("/juegos?texto=" + urllib.parse.quote(titulo))[1]["juegos"][0]["juegoId"]


MOCK = json.load(open(os.path.join(RAIZ, "backend", "GameRank.Services", "Steam", "steam-mock.json"), encoding="utf-8"))
CELESTE = next(j for j in MOCK if j["name"] == "Celeste")
GOW = next(j for j in MOCK if j["steam_appid"] == 1593500)

print("=== 1. Mock, sin sincronizacion al iniciar")
recrear()
api = Api(Steam__Modo="Mock", Steam__IntervaloHoras="0")

s, c, _ = pedir("/generos")
caso("generos de Steam", (200, 9, True), (s, len(c), "Acción" in [g["nombre"] for g in c]))
s, c, _ = pedir("/plataformas")
caso("plataformas", (200, ["linux", "mac", "windows"]), (s, sorted(p["slug"] for p in c)))
s, c, _ = pedir("/juegos")
caso("catalogo completo", (200, 7, False), (s, c["total"], c["parcial"]))
caso("orden por titulo", sorted(titulos(c)), titulos(c))
caso("expone steamAppId", True, all("steamAppId" in j and "rawgId" not in j for j in c["juegos"]))
s, c, _ = pedir("/juegos?texto=witcher")
caso("busqueda local", (200, ["The Witcher 3: Wild Hunt"]), (s, titulos(c)))
caso("no consulto Steam si hay datos locales", "0", sql("SELECT COUNT(*) FROM dbo.SincronizacionApi"))
s, c, _ = pedir("/juegos?texto=celeste")
caso("titulo no cacheado se trae de Steam", (200, ["Celeste"], False), (s, titulos(c), c["parcial"]))
caso("  guarda el id de Steam", CELESTE["steam_appid"], c["juegos"][0]["steamAppId"])
caso("  bitacora de la busqueda", "BUSQUEDA OK 1 0 0 0", bitacora())
esperado = f"{len(CELESTE['genres'])} {sum(CELESTE['platforms'].values())} {len(CELESTE['developers'])}"
caso("  juego persistido con relaciones", esperado, sql("SELECT (SELECT COUNT(*) FROM dbo.JuegoGenero jg JOIN dbo.Juego j ON j.JuegoId=jg.JuegoId WHERE j.Titulo='Celeste'), (SELECT COUNT(*) FROM dbo.JuegoPlataforma jp JOIN dbo.Juego j ON j.JuegoId=jp.JuegoId WHERE j.Titulo='Celeste'), (SELECT COUNT(*) FROM dbo.JuegoDesarrolladora jd JOIN dbo.Juego j ON j.JuegoId=jd.JuegoId WHERE j.Titulo='Celeste')"))
s, c, _ = pedir("/juegos?texto=celeste")
caso("segunda busqueda sale de la base", "1", sql("SELECT COUNT(*) FROM dbo.SincronizacionApi"))
s, c, _ = pedir("/juegos?texto=zzzzzz")
caso("sin resultados", (200, 0, False), (s, c["total"], c["parcial"]))
s, c, _ = pedir("/juegos?texto=mario&generos=accion")
caso("texto con filtros no dispara Steam", (200, "2"), (s, sql("SELECT COUNT(*) FROM dbo.SincronizacionApi")))
s, c, _ = pedir("/juegos?generos=indie")
caso("filtro por genero", ["Celeste", "Hades", "Hollow Knight"], titulos(c))
s, c, _ = pedir("/juegos?generos=aventura,indie")
caso("varios generos (cualquiera)", ["Celeste", "God of War", "Grand Theft Auto V", "Hades", "Hollow Knight", "Portal 2"], titulos(c))
s, c, _ = pedir("/juegos?generos=rol&plataformas=mac")
caso("genero y plataforma (ambos)", ["Hades"], titulos(c))
s, c, _ = pedir("/juegos?plataformas=linux")
caso("filtro por sistema operativo", True, s == 200 and {"Portal 2", "Hollow Knight"} <= set(titulos(c)) and "Hades" not in titulos(c))
s, c, _ = pedir("/juegos?puntuacionMin=4.5")
caso("puntuacion minima", True, s == 200 and all(j["promedioCalificacion"] >= 4.5 for j in c["juegos"]) and c["total"] > 0, c["total"])
s, c, _ = pedir("/juegos?orden=puntuacion")
caso("orden por puntuacion", 200, s, [j["promedioCalificacion"] for j in c["juegos"]][:3])
s, c, _ = pedir("/juegos?orden=lanzamiento&tamanioPagina=2&pagina=2")
caso("paginacion", (200, 2, 2, 2, 8), (s, len(c["juegos"]), c["pagina"], c["tamanioPagina"], c["total"]))
for nombre, ruta in [("orden invalido", "/juegos?orden=xxx"), ("minimo mayor al maximo", "/juegos?puntuacionMin=5&puntuacionMax=2"),
                     ("puntuacion fuera de rango", "/juegos?puntuacionMin=9"), ("puntuacion no numerica", "/juegos?puntuacionMin=abc"),
                     ("texto largo", "/juegos?texto=" + "a" * 101), ("pagina invalida", "/juegos?pagina=0"), ("tamanio invalido", "/juegos?tamanioPagina=500")]:
    s, c, _ = pedir(ruta)
    caso(nombre, (400, True), (s, "mensaje" in c), c["mensaje"])
s, c, _ = pedir("/juegos?texto=" + urllib.parse.quote("'; DROP TABLE dbo.Juego;--"))
caso("texto con SQL no rompe nada", (200, "8"), (s, sql("SELECT COUNT(*) FROM dbo.Juego")))
s, c, _ = pedir("/juegos/1")
caso("ficha completa", (200, 1, 1, 1, 292030), (s, len(c["generos"]), len(c["plataformas"]), len(c["desarrolladoras"]), c["steamAppId"]), c["titulo"])
caso("  fecha sin hora", "2015-05-18", c["fechaLanzamiento"])
caso("  no expone datos internos ni campos de RAWG", False, any(k in c for k in ["fechaSincronizacion", "ratingRawg", "rawgId"]))
sql("UPDATE dbo.Juego SET FechaSincronizacion = '2026-01-01' WHERE JuegoId = 2")
s, c, _ = pedir("/juegos/2")
caso("ficha vencida sin datos en Steam sigue funcionando", (200, "Grand Theft Auto V"), (s, c["titulo"]))
s, c, _ = pedir("/juegos/4")
caso("ficha incompleta se completa desde Steam", (200, GOW["short_description"], len(GOW["developers"])), (s, c["sinopsis"], len(c["desarrolladoras"])))
caso("  con la portada de Steam", True, (c["portadaUrl"] or "").startswith("https://shared.akamai.steamstatic.com/"))
s, c, _ = pedir("/juegos/99999")
caso("juego inexistente", (404, True), (s, "mensaje" in c), c["mensaje"])
api.cerrar()

print("=== 2. Mock, sincronizacion al iniciar")
recrear()
api = Api(Steam__Modo="Mock", Steam__IntervaloHoras="24")
caso("bitacora programada", True, esperar_bitacora("PROGRAMADA"))
caso("  inserta los nuevos y actualiza el existente", f"PROGRAMADA OK {len(MOCK) - 1} 1 0 0", bitacora())
s, c, _ = pedir("/juegos?tamanioPagina=100")
caso("  catalogo completo", 7 + len(MOCK) - 1, c["total"])
s, c, _ = pedir("/generos")
caso("  no duplica generos", 9, len(c))
caso("  desarrolladoras sin repetir", "0", sql("SELECT COUNT(*) FROM (SELECT Slug FROM dbo.Desarrolladora GROUP BY Slug HAVING COUNT(*) > 1) x"))
api.cerrar()
api = Api(Steam__Modo="Mock", Steam__IntervaloHoras="24")
caso("segunda corrida no duplica", True, esperar_bitacora("PROGRAMADA", 2))
caso("  todo actualizado", f"PROGRAMADA OK 0 {len(MOCK)} 0 0", bitacora())
api.cerrar()

print("=== 3. Cliente real contra un servidor que imita a Steam")
estado = {"modo": "ok", "pedidos": [], "contador": 0}
DATOS = {
    8000001: {"type": "game", "name": "Juego Falso", "steam_appid": 8000001, "short_description": "Un juego &amp; <b>falso</b> para pruebas.",
              "header_image": "https://ejemplo.test/falso.jpg", "release_date": {"coming_soon": False, "date": "5 SEPT 2020"},
              "genres": [{"id": "1", "description": "Acción"}, {"id": "77", "description": "Género Nuevo"}],
              "platforms": {"windows": True, "mac": True, "linux": False},
              "developers": ["Estudio Falso, Inc.", "Estudio Falso Inc"], "metacritic": {"score": 80}},
    8000002: {"type": "game", "name": "", "steam_appid": 8000002},
    8000003: {"type": "game", "name": "Juego Futuro", "steam_appid": 8000003, "release_date": {"coming_soon": True, "date": "Próximamente"},
              "genres": [{"id": "23", "description": "Indie"}], "platforms": {"windows": True, "mac": False, "linux": False}, "developers": ["Otro Estudio"]},
    8000004: {"type": "dlc", "name": "Juego Falso Soundtrack", "steam_appid": 8000004},
}
BUSQUEDA = {"ok": [8000001, 8000003, 8000004], "invalido": [8000001, 8000002], "detalle-mixto": [8000001, 8000003]}


class SteamFalso(http.server.BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def responder(self, codigo, cuerpo):
        datos = json.dumps(cuerpo).encode()
        try:
            self.send_response(codigo)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(datos)))
            self.end_headers()
            self.wfile.write(datos)
        except OSError:
            pass

    def do_GET(self):
        estado["pedidos"].append(self.path)
        estado["contador"] += 1
        modo = estado["modo"]
        ruta = urllib.parse.urlparse(self.path)
        consulta = urllib.parse.parse_qs(ruta.query)

        if modo == "500":
            return self.responder(500, {})
        if modo == "429-siempre":
            return self.responder(429, {})
        if modo == "429-2" and estado["contador"] <= 2:
            return self.responder(429, {})
        if modo == "lento" and ruta.path.startswith("/storesearch"):
            time.sleep(4)
        if modo == "lento-detalle" and ruta.path.startswith("/appdetails"):
            time.sleep(4)

        if ruta.path.startswith("/storesearch"):
            ids = BUSQUEDA.get(modo, BUSQUEDA["ok"])
            return self.responder(200, {"total": len(ids), "items": [{"id": i, "name": DATOS[i]["name"], "type": "app"} for i in ids]})
        if ruta.path.startswith("/featuredcategories"):
            return self.responder(200, {"top_sellers": {"items": [{"id": 8000001}, {"id": 8000003}]}, "new_releases": {"items": [{"id": 8000001}]}, "specials": {"items": []}})
        if ruta.path.startswith("/appdetails"):
            appid = int(consulta["appids"][0])
            if modo == "detalle-500" or (modo == "detalle-mixto" and appid != 8000001):
                return self.responder(500, {})
            if appid not in DATOS:
                return self.responder(200, {str(appid): {"success": False}})
            return self.responder(200, {str(appid): {"success": True, "data": DATOS[appid]}})
        self.responder(404, {})


servidor = http.server.ThreadingHTTPServer(("127.0.0.1", 5299), SteamFalso)
threading.Thread(target=servidor.serve_forever, daemon=True).start()
REAL = dict(Steam__Modo="Real", Steam__TiendaUrl="http://127.0.0.1:5299", Steam__IntervaloHoras="0", Steam__TimeoutSegundos="1.5")

recrear()
api = Api(**REAL)
s, c, _ = pedir("/juegos?texto=falso")
caso("trae el juego del servidor", (200, ["Juego Falso"], False), (s, titulos(c), c["parcial"]))
caso("  pide en castellano", True, any("term=falso" in p and "l=spanish" in p for p in estado["pedidos"]))
caso("  guarda los juegos y descarta el DLC", "BUSQUEDA OK 2 0 0 0", bitacora())
caso("  el DLC no queda en la base", "0", sql("SELECT COUNT(*) FROM dbo.Juego WHERE SteamAppId = 8000004"))
s, f, _ = pedir(f"/juegos/{c['juegos'][0]['juegoId']}")
caso("  sinopsis sin HTML ni entidades", "Un juego & falso para pruebas.", f["sinopsis"])
caso("  fecha en castellano convertida", "2020-09-05", f["fechaLanzamiento"])
caso("  generos con uno nuevo", ["Acción", "Género Nuevo"], [g["nombre"] for g in f["generos"]])
caso("  slug del genero nuevo", "genero-nuevo", sql("SELECT Slug FROM dbo.Genero WHERE SteamId = 77"))
caso("  plataformas", ["mac", "windows"], sorted(p["slug"] for p in f["plataformas"]))
caso("  desarrolladoras con el mismo slug se unen", ["estudio-falso-inc"], [d["slug"] for d in f["desarrolladoras"]])
caso("  metacritic y portada", (80, "https://ejemplo.test/falso.jpg"), (f["metacritic"], f["portadaUrl"]))
s, f, _ = pedir(f"/juegos/{id_por_titulo('Juego Futuro')}")
caso("  juego sin fecha ni sinopsis", (200, None, None), (s, f["fechaLanzamiento"], f["sinopsis"]))

estado["modo"] = "invalido"
s, c, _ = pedir("/juegos?texto=noexiste1")
caso("registro invalido no corta el lote", (200, False), (s, c["parcial"]))
caso("  bitacora PARCIAL", "BUSQUEDA PARCIAL 0 1 1 0", bitacora())
caso("  guarda el motivo", True, "slug" in ultimo_mensaje())

estado["modo"] = "detalle-mixto"
s, c, _ = pedir("/juegos?texto=noexiste2")
caso("una ficha que falla no corta el lote", (200, False), (s, c["parcial"]))
caso("  bitacora PARCIAL", "BUSQUEDA PARCIAL 0 1 1 0", bitacora())
caso("  explica el motivo", True, "no respondieron" in ultimo_mensaje())

estado["modo"] = "detalle-500"
s, c, _ = pedir("/juegos?texto=noexiste3")
caso("todas las fichas fallan", (200, True), (s, c["parcial"]))
caso("  bitacora ERROR", "BUSQUEDA ERROR 0 0 0 0", bitacora())

estado["modo"] = "500"
s, c, _ = pedir("/juegos?texto=noexiste4")
caso("Steam caido con error 500", (200, 0, True), (s, c["total"], c["parcial"]))
caso("  bitacora ERROR", "BUSQUEDA ERROR 0 0 0 0", bitacora())
s, c, _ = pedir("/juegos?texto=witcher")
caso("  datos locales siguen disponibles", (200, 1, False), (s, c["total"], c["parcial"]))
sql("UPDATE dbo.Juego SET Sinopsis = NULL WHERE JuegoId = 3")
s, c, _ = pedir("/juegos/3")
caso("  ficha sigue disponible", (200, None), (s, c["sinopsis"]))

estado["modo"] = "lento"
s, c, t = pedir("/juegos?texto=noexiste5")
caso("busqueda lenta: responde parcial", (200, True), (s, c["parcial"]))
caso("  dentro de los 2 segundos", True, t < 2.0, round(t, 2))
caso("  bitacora ERROR con motivo", True, "tiempo" in ultimo_mensaje())

estado["modo"] = "lento-detalle"
s, c, t = pedir("/juegos?texto=noexiste6")
caso("fichas lentas: responde parcial", (200, True), (s, c["parcial"]))
caso("  dentro de los 2 segundos", True, t < 2.0, round(t, 2))

estado["modo"] = "429-siempre"
s, c, t = pedir("/juegos?texto=noexiste7")
caso("429 en busqueda no reintenta", (200, True, True), (s, c["parcial"], t < 2.0), round(t, 2))
api.cerrar()

estado.update(modo="429-2", contador=0)
recrear()
api = Api(**dict(REAL, Steam__IntervaloHoras="24"))
caso("429 en programada reintenta", True, esperar_bitacora("PROGRAMADA"))
caso("  dos reintentos y luego OK", "PROGRAMADA OK 2 0 0 2", bitacora())
caso("  pide los destacados", True, any(p.startswith("/featuredcategories") for p in estado["pedidos"]))
api.cerrar()

estado.update(modo="429-siempre", contador=0)
recrear()
api = Api(**dict(REAL, Steam__IntervaloHoras="24", Steam__MaxReintentos="1"))
caso("429 agota los reintentos", True, esperar_bitacora("PROGRAMADA"))
caso("  bitacora ERROR", "PROGRAMADA ERROR 0 0 0 1", bitacora())
api.cerrar()
servidor.shutdown()

recrear()
api = Api(**dict(REAL, Steam__TiendaUrl="http://127.0.0.1:5298"))
s, c, t = pedir("/juegos?texto=noexiste8")
caso("Steam inaccesible no rompe", (200, True), (s, c["parcial"]))
caso("  dentro de los 2 segundos", True, t < 2.0, round(t, 2))
motivo = ultimo_mensaje()
caso("  bitacora con el motivo", True, "conectar" in motivo or "tiempo" in motivo, motivo)
api.cerrar()

resetear()
print("base final:", sql("SELECT (SELECT COUNT(*) FROM dbo.Juego), (SELECT COUNT(*) FROM dbo.Usuario), (SELECT COUNT(*) FROM dbo.SincronizacionApi)"))
terminar()
