import os
import subprocess
import sys
import tempfile

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(AQUI))
SUITES = ["prueba_auth", "prueba_catalogo", "prueba_valoracion", "prueba_admin", "prueba_respaldo"]


def main():
    if "--borrar-base" not in sys.argv:
        raise SystemExit("Estas pruebas borran y recrean la base GameRank. Para confirmar, agregar --borrar-base.")

    compilacion = subprocess.run(["dotnet", "build", os.path.join(RAIZ, "backend", "GameRank.sln"), "-v", "q"])
    if compilacion.returncode != 0:
        raise SystemExit("No compiló la solución.")

    temporal = tempfile.mkdtemp(prefix="gamerank_pruebas_")
    env = dict(os.environ, GAMERANK_PRUEBAS_BORRAR_BASE="1", GAMERANK_PRUEBAS_TEMPORAL=temporal, PYTHONIOENCODING="utf-8")

    total, fallidas = 0, 0
    for suite in SUITES:
        resultado = subprocess.run([sys.executable, os.path.join(AQUI, suite + ".py")], env=env, capture_output=True, text=True, encoding="utf-8", errors="replace")
        lineas = resultado.stdout.splitlines()
        correctos = sum(1 for l in lineas if l.startswith("OK"))
        malos = [l for l in lineas if l.startswith("FALLA ")]
        total += correctos + len(malos)
        fallidas += len(malos) + (0 if resultado.returncode == 0 or malos else 1)

        print(f"{suite}: {correctos} correctos, {len(malos)} fallidos")
        for linea in malos:
            print("  " + linea)
        if resultado.returncode != 0 and not malos:
            print(resultado.stdout[-800:])
            print(resultado.stderr[-800:])

    subprocess.run(["sqlcmd", "-S", os.environ.get("GAMERANK_PRUEBAS_SERVIDOR", "."), "-E", "-b", "-i", "00_run_all.sql"],
                   cwd=os.path.join(RAIZ, "database"), capture_output=True)

    errores = 0
    ruta_log = os.path.join(temporal, "api.log")
    if os.path.isfile(ruta_log):
        with open(ruta_log, encoding="utf-8", errors="replace") as log:
            errores = sum(1 for l in log if l.startswith("fail:"))

    print(f"Total: {total} casos, {fallidas} fallidos. Errores no controlados en la API: {errores}.")
    sys.exit(1 if fallidas or errores else 0)


if __name__ == "__main__":
    main()
