# GameRank - Backend

API REST de GameRank en ASP.NET Core (.NET 10) sobre SQL Server.

## Capas

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `GameRank.BE` | Entidades | - |
| `GameRank.Services` | Excepciones, hash, tokens, CSV, archivos y cliente de RAWG | - |
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

Al iniciar, la API sincroniza el catálogo. Con el mock de RAWG pasa de 7 a 26 juegos.

## Configuración

Los valores por defecto están en `GameRank.API/appsettings.json`. Para cambiarlos en una sola máquina se usan User Secrets, que no se suben al repositorio.

| Clave | Por defecto | Para qué sirve |
|---|---|---|
| `ConnectionStrings:GameRank` | Instancia local `.` | Conexión a la base |
| `Rawg:Modo` | `Mock` | `Mock` usa datos locales, `Real` llama a RAWG |
| `Rawg:ApiKey` | Vacío | Clave de RAWG, solo para modo `Real` |
| `Rawg:IntervaloHoras` | `24` | Frecuencia de la sincronización; `0` la desactiva |
| `Rawg:PaginasPorCiclo` | `3` | Páginas por sincronización |
| `Rawg:TamanioLote` | `40` | Juegos por página |
| `Rawg:MaxReintentos` | `3` | Reintentos ante un error 429 |
| `Rawg:TimeoutSegundos` | `1.5` | Tiempo máximo de espera a RAWG |
| `Respaldo:Directorio` | Vacío | Carpeta de respaldos. Si está vacía, usa la subcarpeta `GameRank` de la carpeta de backups de SQL Server |
| `Cors:Origenes` | Puerto 5500 local | Orígenes autorizados para el front |

Ejemplo para quien usa LocalDB:

```bash
dotnet user-secrets set "ConnectionStrings:GameRank" "Server=(localdb)\MSSQLLocalDB;Database=GameRank;Trusted_Connection=True;TrustServerCertificate=True" --project backend/GameRank.API
```

Para que la API pueda calcular el checksum y descargar respaldos, necesita permiso de lectura sobre la carpeta de respaldos. SQL Server necesita permiso de escritura sobre esa misma carpeta.

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

`generos` y `plataformas` reciben slugs separados por coma. `orden` admite `titulo`, `puntuacion` o `lanzamiento`. Si `parcial` es `true`, RAWG no respondió y los resultados son solo los locales.

### Calificaciones, reseñas y listas

| Método y ruta | Sesión | Cuerpo o parámetros | Respuesta |
|---|---|---|---|
| `PUT /api/juegos/{id}/calificacion` | Sí | `puntuacion` (1 a 5) | `{ promedioCalificacion, cantidadCalificaciones }` |
| `DELETE /api/juegos/{id}/calificacion` | Sí | - | Igual que el anterior |
| `GET /api/juegos/{id}/resenas` | No | `pagina`, `tamanioPagina` | `{ total, pagina, tamanioPagina, resenas }` |
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
