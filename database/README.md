# GAME RANK — Base de datos

Modelo relacional de GAME RANK sobre **Microsoft SQL Server 2019+**.

![DER](DER-game-rank.png)

## Scripts

| Script | Contenido |
|---|---|
| `00_run_all.sql` | Ejecuta todo en orden (modo SQLCMD) |
| `01_create_database.sql` | Crea la base `GameRank` (recovery FULL para backups de log) |
| `02_tables.sql` | 18 tablas con PK, FK, UNIQUE, CHECK, DEFAULT e índices |
| `03_views.sql` | `vw_JuegoMetricas`, `vw_FeedResenas`, `vw_ResenasReportadas` |
| `04_procedures.sql` | Procedimientos por módulo (M01–M07), uno o más por caso de uso |
| `05_seed.sql` | Roles, estados de lista, catálogo de ejemplo con ids reales de RAWG, usuarios y actividad de prueba |
| `06_carga_masiva.sql` | Opcional: simulación de volumen (50k juegos, 20k usuarios, ~1M calificaciones, 100k reseñas) |

```bash
cd database
sqlcmd -S "(localdb)\MSSQLLocalDB" -b -i 00_run_all.sql
```

En SSMS: abrir `00_run_all.sql` y activar *Query > SQLCMD Mode*. Para la carga masiva, descomentar la última línea o ejecutar `06_carga_masiva.sql` aparte (tarda unos 30 s).

## Tablas por módulo

| Módulo | Tablas |
|---|---|
| M01 Autenticación y Seguridad | `Rol`, `Usuario`, `Sesion` |
| M02/M03 Catálogo e Integración RAWG | `Juego`, `Genero`, `Plataforma`, `Desarrolladora`, `JuegoGenero`, `JuegoPlataforma`, `JuegoDesarrolladora`, `SincronizacionApi` |
| M04 Valoración y Reseñas | `Calificacion`, `Resena`, `ReporteResena` |
| M05 Biblioteca Personal | `EstadoLista`, `BibliotecaUsuario` |
| M07/M08 Administración y Respaldo | `Auditoria`, `Respaldo` |

## Decisiones de diseño

- **Contraseñas**: `Usuario.PasswordHash` guarda el hash que genera el backend (bcrypt/argon2); nunca texto plano. De las sesiones se guarda solo el SHA-256 del token (`Sesion.TokenHash`).
- **Ley 25.326**: `AceptaPrivacidad` (CHECK = 1), `FechaConsentimiento` y `VersionPolitica` dejan constancia del consentimiento. `sp_EliminarUsuario` implementa el derecho de supresión.
- **Unicidad usuario–juego**: `Calificacion` y `BibliotecaUsuario` usan PK compuesta `(UsuarioId, JuegoId)`; `Resena` tiene `UNIQUE (UsuarioId, JuegoId)`. Así se cumplen CU07, CU08 y CU10 sin duplicados.
- **Datos de la API**: cada entidad de catálogo guarda su `RawgId` (UNIQUE) para el upsert de la sincronización (`sp_UpsertJuegoDesdeApi`, que recibe el JSON de RAWG tal cual). Los campos opcionales de RAWG (sinopsis, portada, fecha, métricas) admiten NULL.
- **Moderación**: las reseñas se ocultan con `Estado = 'O'` o se eliminan; toda acción administrativa queda en `Auditoria` (sin FK a la entidad afectada, porque puede haber sido eliminada).
- **Cascadas**: borrar un usuario o un juego borra en cascada sus calificaciones, reseñas, listas y sesiones. `ReporteResena.UsuarioReportanteId` no tiene cascada porque SQL Server no admite múltiples rutas de cascada; lo resuelve `sp_EliminarUsuario`.
- **Errores**: los procedimientos lanzan `THROW 50000 + código HTTP` (por ejemplo, 50409 = 409 Conflict, 50403 = 403 Forbidden), así el backend traduce el error directamente a la respuesta HTTP.

## Rendimiento (con `06_carga_masiva.sql`)

`sp_BuscarCatalogo` con texto, dos géneros, puntuación mínima y orden por promedio, sobre 50k juegos y ~1M calificaciones: **~0,5 s** en LocalDB, dentro del límite de 2 s del RNF de rendimiento.

## Usuarios de prueba

Los usuarios del seed tienen un `PasswordHash` de relleno (`SEED_SIN_LOGIN`) y no permiten iniciar sesión. Para probar el login, registrarse desde la aplicación y promover la cuenta:

```sql
EXEC dbo.sp_AsignarRol @SuperAdminId = 1, @UsuarioId = <id>, @RolId = 3;
```
