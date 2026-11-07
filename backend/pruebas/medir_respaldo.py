import json
import os
import shutil
import time
import urllib.error
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
        r = urllib.request.urlopen(req, timeout=500)
    except urllib.error.HTTPError as e:
        r = e
    bruto = r.read()
    return (r.code if hasattr(r, "code") else r.status), (json.loads(bruto.decode()) if bruto else None), time.perf_counter() - inicio


DIR = os.path.join(sql("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(400))", "master"), "GameRank")
if os.path.isdir(DIR):
    raise SystemExit("ya existe la carpeta")

api = arrancar()

_, c, _ = pedir("POST", "/auth/login", {"email": "medicion@prueba.test", "password": "Clave1234"})
t = c["token"]

for tipo in ["COMPLETO", "DIFERENCIAL", "LOG", "EXPORTACION"]:
    s, c, d = pedir("POST", "/admin/respaldos", {"tipo": tipo}, t)
    print(f"{tipo:12} {s} {d:6.1f} s  {c.get('tamanioBytes', 0) / 1048576:8.1f} MB  {c.get('estado')}")
    if tipo == "COMPLETO":
        completo = c["respaldoId"]

antes = sql("SELECT COUNT(*) FROM dbo.Calificacion")
s, c, d = pedir("POST", f"/admin/respaldos/{completo}/restauracion", {"confirmacion": "RESTAURAR"}, t)
print(f"RESTAURACION {s} {d:6.1f} s  {c}")
print("notas antes y despues:", antes, sql("SELECT COUNT(*) FROM dbo.Calificacion"))
s, c, d = pedir("GET", "/juegos?texto=a", token=t)
print("catalogo despues:", s, f"{d*1000:.0f} ms", c["total"])

detener(api)
shutil.rmtree(DIR)
