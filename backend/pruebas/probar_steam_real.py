import json
import os
import sys
import time
import urllib.parse
import urllib.request

if "--borrar-base" in sys.argv:
    os.environ["GAMERANK_PRUEBAS_BORRAR_BASE"] = "1"

from comun import BASE, arrancar, detener, resetear, sql


def pedir(ruta):
    inicio = time.perf_counter()
    with urllib.request.urlopen(BASE + ruta, timeout=30) as r:
        return r.status, json.loads(r.read().decode()), time.perf_counter() - inicio


def esperar_sincronizacion():
    for _ in range(240):
        if sql("SELECT COUNT(*) FROM dbo.SincronizacionApi WHERE Origen = 'PROGRAMADA' AND Estado <> 'EN_CURSO' AND FechaInicio > DATEADD(MINUTE, -10, SYSUTCDATETIME())") != "0":
            return True
        time.sleep(0.5)
    return False


resetear()
sql("DELETE FROM dbo.SincronizacionApi")
inicio = time.perf_counter()
api = arrancar(Steam__Modo="Real", Steam__IntervaloHoras="24")

print("sincronizacion terminada:", esperar_sincronizacion(), f"en {time.perf_counter() - inicio:.1f} s")
print("bitacora (origen, estado, insertados, actualizados, errores, reintentos):")
print(" ", sql("SELECT TOP 1 Origen, Estado, RegistrosInsertados, RegistrosActualizados, RegistrosConError, Reintentos FROM dbo.SincronizacionApi ORDER BY SincronizacionId DESC"))
print("  motivo:", sql("SELECT TOP 1 ISNULL(MensajeError, '-') FROM dbo.SincronizacionApi ORDER BY SincronizacionId DESC"))

s, c, t = pedir("/juegos?tamanioPagina=100")
print(f"catalogo: {c['total']} juegos")
s, g, _ = pedir("/generos")
print("generos:", [x["nombre"] for x in g])
s, p, _ = pedir("/plataformas")
print("plataformas:", [x["nombre"] for x in p])

for texto in ["stardew valley", "terraria"]:
    s, c, t = pedir("/juegos?texto=" + urllib.parse.quote(texto))
    print(f"busqueda '{texto}': {s}, {t:.2f} s, parcial={c['parcial']}, total={c['total']}, primeros={[j['titulo'] for j in c['juegos']][:3]}")
    print("  bitacora:", sql("SELECT TOP 1 Origen, Estado, RegistrosInsertados, RegistrosActualizados, RegistrosConError FROM dbo.SincronizacionApi ORDER BY SincronizacionId DESC"))

s, c, _ = pedir("/juegos?texto=" + urllib.parse.quote("stardew valley"))
if c["juegos"]:
    s, f, t = pedir(f"/juegos/{c['juegos'][0]['juegoId']}")
    print(f"ficha: {s}, {t:.2f} s")
    for clave in ["titulo", "steamAppId", "fechaLanzamiento", "metacritic", "portadaUrl", "sinopsis"]:
        valor = f[clave]
        print(f"  {clave}: {valor[:90] if isinstance(valor, str) else valor}")
    print("  generos:", [x["nombre"] for x in f["generos"]])
    print("  plataformas:", [x["nombre"] for x in f["plataformas"]])
    print("  desarrolladoras:", [x["nombre"] for x in f["desarrolladoras"]])

s, f, t = pedir("/juegos/4")
print(f"ficha incompleta del seed (God of War): {t:.2f} s, sinopsis completada={f['sinopsis'] is not None}, desarrolladoras={[x['nombre'] for x in f['desarrolladoras']]}")
print("juegos sin fecha:", sql("SELECT COUNT(*) FROM dbo.Juego WHERE FechaLanzamiento IS NULL"), "de", sql("SELECT COUNT(*) FROM dbo.Juego"))

detener(api)
resetear()
