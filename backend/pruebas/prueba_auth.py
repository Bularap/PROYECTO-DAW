import json
import urllib.error
import urllib.request

from comun import BASE, arrancar, caso, detener, resetear, sql, terminar


def pedir(metodo, ruta, cuerpo=None, token=None, crudo=None, origen=None):
    datos = crudo if crudo is not None else (json.dumps(cuerpo).encode() if cuerpo is not None else None)
    req = urllib.request.Request(BASE + ruta, data=datos, method=metodo)
    if datos is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    if origen:
        req.add_header("Origin", origen)
    try:
        r = urllib.request.urlopen(req)
    except urllib.error.HTTPError as e:
        r = e
    texto = r.read().decode()
    try:
        cuerpo_r = json.loads(texto) if texto else None
    except ValueError:
        cuerpo_r = texto
    return r.status if hasattr(r, "status") else r.code, cuerpo_r, r.headers

resetear()
api = arrancar()

MAIL = "ana@prueba.test"
CLAVE = "Clave1234"
reg = {"email": MAIL, "nombreUsuario": "ana_prueba", "password": CLAVE, "aceptaPrivacidad": True}

s, c, _ = pedir("POST", "/auth/registro", reg)
caso("registro correcto", 201, s, c.get("usuario"))
token = c["token"]

s, c, h = pedir("POST", "/auth/registro", reg, origen="http://127.0.0.1:5500")
caso("correo duplicado (error de SP traducido)", 409, s, c)
caso("  CORS presente en respuesta de error", "http://127.0.0.1:5500", h.get("Access-Control-Allow-Origin"))
s, c, _ = pedir("POST", "/auth/registro", {**reg, "email": "otra@prueba.test"})
caso("nombre duplicado", 409, s, c)
s, c, _ = pedir("POST", "/auth/registro", {**reg, "email": "x@prueba.test", "nombreUsuario": "x_prueba", "aceptaPrivacidad": False})
caso("sin consentimiento", 400, s, c)
s, c, _ = pedir("POST", "/auth/registro", {**reg, "email": "x@prueba.test", "nombreUsuario": "x_prueba", "password": "corta1"})
caso("contrasena corta", 400, s, c)
s, c, _ = pedir("POST", "/auth/registro", {**reg, "email": "x@prueba.test", "nombreUsuario": "x_prueba", "password": "sololetras"})
caso("contrasena sin numero", 400, s, c)
s, c, _ = pedir("POST", "/auth/registro", {**reg, "email": "no-es-correo", "nombreUsuario": "x_prueba"})
caso("correo invalido", 400, s, c)
s, c, _ = pedir("POST", "/auth/registro", {**reg, "email": "x@sinpunto", "nombreUsuario": "x_prueba"})
caso("correo sin dominio con punto", 400, s, c)
s, c, _ = pedir("POST", "/auth/registro", crudo=b"{mal json")
caso("JSON invalido", 400, s, c)

s, c, _ = pedir("POST", "/auth/login", {"email": MAIL, "password": CLAVE})
caso("login correcto", 200, s, c.get("usuario"))
token2 = c["token"]
s, c1, _ = pedir("POST", "/auth/login", {"email": MAIL, "password": "Incorrecta1"})
caso("contrasena incorrecta", 401, s, c1)
s, c2, _ = pedir("POST", "/auth/login", {"email": "nadie@prueba.test", "password": "Incorrecta1"})
caso("correo inexistente", 401, s, c2)
caso("  mismo mensaje en ambos casos", c1, c2)
s, c, _ = pedir("POST", "/auth/login", {"email": MAIL})
caso("campos incompletos", 400, s, c)
s, c, _ = pedir("POST", "/auth/login", {"email": "lucia@gamerank.test", "password": "Clave1234"})
caso("usuario del seed sin hash real", 401, s, c)

s, c, _ = pedir("GET", "/auth/yo", token=token2)
caso("yo con token", 200, s, c)
s, c, _ = pedir("GET", "/auth/yo")
caso("yo sin token", 401, s, c)
s, c, _ = pedir("GET", "/auth/yo", token="inventado")
caso("yo con token inventado", 401, s, c)

s, c, _ = pedir("GET", "/moderacion/usuarios-bloqueados", token=token2)
caso("jugador en endpoint de admin", 403, s, c)
sql(f"UPDATE dbo.Usuario SET RolId = 2 WHERE Email = '{MAIL}'")
s, c, _ = pedir("GET", "/moderacion/usuarios-bloqueados", token=token2)
caso("administrador en endpoint de admin", 200, s)
sql(f"UPDATE dbo.Usuario SET RolId = 3 WHERE Email = '{MAIL}'")
s, c, _ = pedir("GET", "/moderacion/usuarios-bloqueados", token=token2)
caso("super admin en endpoint de admin", 200, s)
sql(f"UPDATE dbo.Usuario SET RolId = 1 WHERE Email = '{MAIL}'")

s, c, _ = pedir("POST", "/auth/logout", token=token2)
caso("logout", 204, s)
s, c, _ = pedir("GET", "/auth/yo", token=token2)
caso("token tras logout", 401, s, c)
s, c, _ = pedir("POST", "/auth/logout", token=token2)
caso("logout repetido sin error", 204, s)
s, c, _ = pedir("POST", "/auth/logout", token="inventado")
caso("logout con token invalido sin error", 204, s)
s, c, _ = pedir("GET", "/auth/yo", token=token)
caso("la otra sesion sigue activa", 200, s)

sql(f"UPDATE dbo.Usuario SET Bloqueado = 1 WHERE Email = '{MAIL}'")
s, c, _ = pedir("POST", "/auth/login", {"email": MAIL, "password": CLAVE})
caso("cuenta bloqueada con clave correcta", 403, s, c)
s, c, _ = pedir("POST", "/auth/login", {"email": MAIL, "password": "Incorrecta1"})
caso("cuenta bloqueada con clave incorrecta", 401, s, c)
s, c, _ = pedir("GET", "/auth/yo", token=token)
caso("sesion activa de cuenta bloqueada", 401, s, c)

hash_guardado = sql(f"SELECT PasswordHash FROM dbo.Usuario WHERE Email = '{MAIL}'")
caso("contrasena guardada con BCrypt", True, hash_guardado.startswith("$2") and CLAVE not in hash_guardado)
sesiones = sql(f"SELECT TokenHash FROM dbo.Sesion s JOIN dbo.Usuario u ON u.UsuarioId = s.UsuarioId WHERE u.Email = '{MAIL}'").split()
caso("token no guardado en claro", True, token not in sesiones and all(len(x) == 64 for x in sesiones) and len(sesiones) == 2)

print("consentimiento:", sql(f"SELECT AceptaPrivacidad, VersionPolitica FROM dbo.Usuario WHERE Email = '{MAIL}'"))
print("limpieza:", sql("DELETE FROM dbo.Usuario WHERE Email LIKE '%@prueba.test'; SELECT COUNT(*) FROM dbo.Usuario;"))
detener(api)
resetear()
terminar()
