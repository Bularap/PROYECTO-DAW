# GameRank - Backend

API REST de GameRank en ASP.NET Core (.NET 10) sobre SQL Server.

## Capas

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `GameRank.BE` | Entidades | - |
| `GameRank.Services` | Excepciones, hash, tokens, CSV, archivos y cliente de Steam | - |
| `GameRank.DAL` | Acceso a datos con ADO.NET y procedimientos almacenados | BE, Services |
| `GameRank.BLL` | Reglas de negocio | BE, DAL, Services |
| `GameRank.API` | Controladores, autenticación y traducción de errores a HTTP | BE, BLL, Services |

`GameRank.API` no puede usar `GameRank.DAL` directamente: el proyecto tiene deshabilitadas las referencias transitivas.

## Puesta en marcha

1. Crear la base desde la carpeta `database`:

```bash
sqlcmd -S "." -E -b -i 00_run_all.sql
```

2. Levantar la API desde la raíz del repositorio. Queda en `http://localhost:5251`.

```bash
dotnet run --project backend/GameRank.API
```

3. Comprobar que responde: `GET http://localhost:5251/api/salud`.

Volver a correr `00_run_all.sql` borra y recrea la base.

Al iniciar, la API sincroniza el catálogo con los juegos destacados de Steam. En modo `Mock` usa los 21 juegos de `GameRank.Services/Steam/steam-mock.json`.

## Configuración

Los valores por defecto están en `GameRank.API/appsettings.json`. Para cambiarlos en una sola máquina se usan User Secrets, que no se suben al repositorio.

| Clave | Por defecto | Para qué sirve |
|---|---|---|
| `ConnectionStrings:GameRank` | Instancia local `.` | Conexión a la base |
| `Steam:Modo` | `Real` | `Real` consulta la tienda de Steam; `Mock` usa datos locales, para trabajar sin conexión o si Steam falla |
| `Steam:TiendaUrl` | Tienda de Steam | Dirección de la API de la tienda |
| `Steam:IntervaloHoras` | `24` | Frecuencia de la sincronización; `0` la desactiva |
| `Steam:MaxJuegosPorCiclo` | `40` | Juegos destacados que se traen en cada sincronización |
| `Steam:MaxReintentos` | `3` | Reintentos ante un error 429 en la sincronización programada |
| `Steam:TimeoutSegundos` | `1.5` | Tiempo máximo de espera a Steam. En la búsqueda es el tiempo total |
| `Respaldo:Directorio` | Vacío | Carpeta de respaldos. Si está vacía, usa la subcarpeta `GameRank` de la carpeta de backups de SQL Server |
| `Cors:Origenes` | Puerto 5500 local | Orígenes autorizados para el front |

Steam no requiere clave: se usan los endpoints públicos de la tienda (`appdetails`, `storesearch` y `featuredcategories`), que no tienen documentación oficial.

Ejemplo para quien usa LocalDB:

```bash
dotnet user-secrets set "ConnectionStrings:GameRank" "Server=(localdb)\MSSQLLocalDB;Database=GameRank;Trusted_Connection=True;TrustServerCertificate=True" --project backend/GameRank.API
```

Para que la API pueda calcular el checksum y descargar respaldos, necesita permiso de lectura sobre la carpeta de respaldos. SQL Server necesita permiso de escritura sobre esa misma carpeta.

## Pruebas

Las pruebas están en `backend/pruebas` y requieren Python 3 y `sqlcmd`. Ejercitan la API en ejecución contra una base real.

**Borran y recrean la base `GameRank`**, por eso piden un parámetro de confirmación. Al terminar la dejan con los datos del seed. La API no debe estar corriendo.

```bash
python backend/pruebas/correr_todo.py --borrar-base
```

| Archivo | Qué cubre | Casos |
|---|---|---|
| `prueba_auth.py` | Registro, login, logout, sesiones y bloqueo | 32 |
| `prueba_catalogo.py` | Búsqueda, filtros, ficha y sincronización, con el mock y con un servidor que imita a Steam | 85 |
| `prueba_valoracion.py` | Calificaciones, reseñas, reportes, listas, feed, reseñas recientes y perfil | 112 |
| `prueba_admin.py` | Permisos, moderación, roles, auditoría y baja de cuenta | 89 |
| `prueba_respaldo.py` | Respaldo, exportación CSV y restauración, incluidas las fallas | 88 |

Para otra instancia de SQL Server, definir la variable `GAMERANK_PRUEBAS_SERVIDOR`, por ejemplo con `(localdb)\MSSQLLocalDB`.

`prueba_respaldo.py` crea la subcarpeta `GameRank` en la carpeta de backups de SQL Server y la borra al terminar. Si esa subcarpeta ya existe, no se ejecuta, para no borrar respaldos reales.

Para comprobar la conexión con Steam real (hace unos 40 pedidos a la tienda y también recrea la base):

```bash
python backend/pruebas/probar_steam_real.py --borrar-base
```

Para repetir las mediciones de rendimiento, primero cargar el volumen y después correr los dos scripts:

```bash
sqlcmd -S "." -E -b -i database/06_carga_masiva.sql
```

```bash
python backend/pruebas/medir_rendimiento.py
```

```bash
python backend/pruebas/medir_respaldo.py
```

## Primer Super Administrador

Los usuarios del seed no pueden iniciar sesión. Hay que registrarse desde la API y promover la cuenta:

```sql
EXEC dbo.sp_AsignarRol @SuperAdminId = 1, @UsuarioId = <id>, @RolId = 3;
```

## Convenciones de la API

- **Sesión:** `POST /api/auth/registro` y `POST /api/auth/login` devuelven `token` y `usuario`. El token se envía en la cabecera `Authorization: Bearer <token>`.
- **Errores:** todos llegan como `{ "mensaje": "..." }`, con 400, 401, 403, 404, 409 o 500 según corresponda.
- **Fechas:** con hora, en UTC y terminadas en `Z`. Las fechas de lanzamiento son `yyyy-MM-dd`.
- **Paginación:** parámetros `pagina` y `tamanioPagina`. Una página posterior a la última devuelve `total` en 0.
- **Texto de usuarios:** el backend quita las etiquetas HTML, pero el front debe mostrarlo con `textContent`.

## Endpoints

Sesión: **No** es público, **Sí** requiere sesión, **Admin** requiere Administrador o Super Administrador, **Super** requiere Super Administrador.

### Autenticación

| Método y ruta | Sesión | Cuerpo | Respuesta |
|---|---|---|---|
| `POST /api/auth/registro` | No | `email`, `nombreUsuario`, `password`, `aceptaPrivacidad` | 201 |
| `POST /api/auth/login` | No | `email`, `password` | 200 |
| `POST /api/auth/logout` | Token opcional | - | 204 |
| `GET /api/auth/yo` | Sí | - | 200 |
| `POST /api/auth/baja` | Sí | `password` | 204 |

La contraseña debe tener entre 8 y 72 caracteres, con al menos una letra y un número.

### Catálogo

| Método y ruta | Sesión | Parámetros | Respuesta |
|---|---|---|---|
| `GET /api/juegos` | No | `texto`, `generos`, `plataformas`, `puntuacionMin`, `puntuacionMax`, `orden`, `pagina`, `tamanioPagina` | `{ total, pagina, tamanioPagina, parcial, juegos }` |
| `GET /api/juegos/{id}` | No | - | Ficha con `generos`, `plataformas` y `desarrolladoras` |
| `GET /api/generos` | No | - | Lista |
| `GET /api/plataformas` | No | - | Lista |
| `GET /api/estados-lista` | No | - | Lista |

`generos` y `plataformas` reciben slugs separados por coma. `orden` admite `titulo`, `puntuacion` o `lanzamiento`. Si `parcial` es `true`, Steam no respondió y los resultados son solo los locales.

Cada juego trae su `steamAppId` y sus `generos`. Las plataformas son `windows`, `mac` y `linux`, y los géneros vienen en castellano, con slugs como `accion`, `aventura`, `rol` o `indie`.

### Calificaciones, reseñas y listas

| Método y ruta | Sesión | Cuerpo o parámetros | Respuesta |
|---|---|---|---|
| `PUT /api/juegos/{id}/calificacion` | Sí | `puntuacion` (1 a 5) | `{ promedioCalificacion, cantidadCalificaciones }` |
| `DELETE /api/juegos/{id}/calificacion` | Sí | - | Igual que el anterior |
| `GET /api/juegos/{id}/resenas` | No | `pagina`, `tamanioPagina` | `{ total, pagina, tamanioPagina, resenas }` |
| `GET /api/resenas` | No | `pagina`, `tamanioPagina` | Reseñas recientes de todos los juegos, con la misma forma que el feed |
| `POST /api/juegos/{id}/resenas` | Sí | `contenido` (10 a 2000 caracteres) | 201 con la reseña |
| `PUT /api/resenas/{id}` | Sí | `contenido` | 200 con la reseña |
| `DELETE /api/resenas/{id}` | Sí | - | 204 |
| `POST /api/resenas/{id}/reportes` | Sí | `motivo` (`SPAM`, `OFENSIVO`, `SPOILER`, `OTRO`), `comentario` | 204 |
| `PUT /api/juegos/{id}/biblioteca` | Sí | `estadoListaId` | 204 |
| `DELETE /api/juegos/{id}/biblioteca` | Sí | - | 204 |
| `GET /api/juegos/{id}/mi-estado` | Sí | - | `{ puntuacion, estadoListaId, resenaId, resenaContenido }` |

### Perfil

| Método y ruta | Sesión | Parámetros | Respuesta |
|---|---|---|---|
| `GET /api/usuarios/{id}` | Sí | - | Métricas, `listas`, `calificaciones` y `resenas` |
| `GET /api/usuarios/{id}/biblioteca` | Sí | `estadoListaId` | Lista |

### Moderación

| Método y ruta | Sesión | Cuerpo o parámetros | Respuesta |
|---|---|---|---|
| `GET /api/moderacion/resenas` | Admin | `filtro` (`reportadas`, `recientes`, `ocultas`), `pagina`, `tamanioPagina` | `{ total, pagina, tamanioPagina, items }` |
| `PUT /api/moderacion/resenas/{id}` | Admin | `accion` (`OCULTAR`, `MOSTRAR`), `motivo` | 204 |
| `DELETE /api/moderacion/resenas/{id}` | Admin | `motivo` en la URL | 204 |
| `PUT /api/moderacion/usuarios/{id}/bloqueo` | Admin | `bloquear`, `motivo` | 204 |
| `GET /api/moderacion/usuarios-bloqueados` | Admin | `pagina`, `tamanioPagina` | `{ total, pagina, tamanioPagina, items }` |

El motivo es obligatorio para ocultar, eliminar y bloquear.

### Administración

| Método y ruta | Sesión | Cuerpo o parámetros | Respuesta |
|---|---|---|---|
| `GET /api/admin/usuarios` | Super | `texto`, `pagina`, `tamanioPagina` | `{ total, pagina, tamanioPagina, items }` |
| `PUT /api/admin/usuarios/{id}/rol` | Super | `rolId` | 204 |
| `GET /api/admin/roles` | Super | - | Lista |
| `GET /api/admin/auditoria` | Super | `pagina`, `tamanioPagina` | `{ total, pagina, tamanioPagina, items }` |
| `GET /api/admin/respaldos` | Super | `pagina`, `tamanioPagina` | `{ total, pagina, tamanioPagina, items }` |
| `POST /api/admin/respaldos` | Super | `tipo` (`COMPLETO`, `DIFERENCIAL`, `LOG`, `EXPORTACION`) | 201 con el respaldo |
| `GET /api/admin/respaldos/{id}/descarga` | Super | - | Archivo |
| `POST /api/admin/respaldos/{id}/restauracion` | Super | `confirmacion` con el valor `RESTAURAR` | 200 con los identificadores usados |

### Estado

| Método y ruta | Sesión | Respuesta |
|---|---|---|
| `GET /api/salud` | No | `{ base, juegos, usuarios }` |
