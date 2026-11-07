import os
import socket
import subprocess
import sys
import tempfile
import time
import urllib.request

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(AQUI))
API_DIR = os.path.join(RAIZ, "backend", "GameRank.API")
EXE = os.path.join(API_DIR, "bin", "Debug", "net10.0", "GameRank.API.exe")
PUERTO = 5251
BASE = f"http://localhost:{PUERTO}/api"
SERVIDOR = os.environ.get("GAMERANK_PRUEBAS_SERVIDOR", ".")
CADENA = f"Server={SERVIDOR};Database=GameRank;Trusted_Connection=True;TrustServerCertificate=True"
TEMPORAL = os.environ.get("GAMERANK_PRUEBAS_TEMPORAL") or tempfile.mkdtemp(prefix="gamerank_pruebas_")
RUTA_LOG = os.path.join(TEMPORAL, "api.log")

fallas = 0


def caso(nombre, esperado, obtenido, detalle=""):
    global fallas
    ok = esperado == obtenido
    fallas += 0 if ok else 1
    print(("OK   " if ok else "FALLA"), nombre, "->", ascii(obtenido), ascii(detalle) if detalle != "" else "")


def terminar():
    print("FALLAS:", fallas)
    sys.exit(1 if fallas else 0)


def sql(q, base="GameRank"):
    r = subprocess.run(["sqlcmd", "-S", SERVIDOR, "-E", "-d", base, "-b", "-W", "-h", "-1", "-f", "65001", "-Q", "SET NOCOUNT ON;" + q],
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.stdout.strip()


def resetear():
    if os.environ.get("GAMERANK_PRUEBAS_BORRAR_BASE") != "1":
        raise SystemExit("Estas pruebas borran la base GameRank. Ejecutarlas con: python backend/pruebas/correr_todo.py --borrar-base")

    r = subprocess.run(["sqlcmd", "-S", SERVIDOR, "-E", "-b", "-i", "00_run_all.sql"], cwd=os.path.join(RAIZ, "database"),
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        raise SystemExit("No se pudo recrear la base: " + r.stdout[-800:])


def puerto_ocupado():
    with socket.socket() as s:
        s.settimeout(0.5)
        return s.connect_ex(("127.0.0.1", PUERTO)) == 0


def arrancar(**extra):
    if puerto_ocupado():
        raise SystemExit(f"El puerto {PUERTO} está ocupado. Cerrar la API antes de correr las pruebas.")
    if not os.path.isfile(EXE):
        raise SystemExit("Falta compilar la API: dotnet build backend/GameRank.sln")

    env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development", ASPNETCORE_URLS=f"http://localhost:{PUERTO}",
               ConnectionStrings__GameRank=CADENA, Steam__Modo="Mock", Steam__IntervaloHoras="0")
    env.update(extra)

    log = open(RUTA_LOG, "a", encoding="utf-8")
    proceso = subprocess.Popen([EXE], cwd=API_DIR, env=env, stdout=log, stderr=log)
    for _ in range(60):
        try:
            urllib.request.urlopen(BASE + "/generos")
            return proceso
        except Exception:
            time.sleep(0.25)

    proceso.kill()
    raise SystemExit("La API no arrancó. Revisar " + RUTA_LOG)


def detener(proceso):
    proceso.kill()
    proceso.wait()
