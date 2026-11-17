# Secciones para el documento técnico

Texto para sumar al documento técnico en Google Docs. Los puntos marcados como pendientes deben revisarse antes de entregar.

## Arquitectura

GAME RANK utiliza una arquitectura cliente-servidor. El cliente es una aplicación web en HTML, CSS y JavaScript que se comunica mediante fetch con una API REST propia, desarrollada en ASP.NET Core sobre .NET 10. La API accede a una base de datos Microsoft SQL Server y a la API de la tienda de Steam como fuente externa de datos.

### Capas del backend

El backend se organiza en una solución con cinco proyectos:

| Capa | Responsabilidad |
|---|---|
| BE (Business Entities) | Define las entidades que comparten las demás capas. |
| Services | Reúne los servicios transversales: excepciones, hash de contraseñas, tokens, exportación CSV, manejo de archivos y cliente de la API de Steam. |
| DAL (Data Access Layer) | Accede a la base de datos con ADO.NET, por medio de procedimientos almacenados. |
| BLL (Business Logic Layer) | Aplica las reglas de negocio y coordina el acceso a datos y a los servicios. |
| API | Expone los endpoints, autentica los pedidos y traduce los errores a códigos HTTP. |

La API depende de BLL, BE y Services, pero no de DAL. Esta restricción se aplica desde la configuración del proyecto, por lo que un controlador no puede acceder a los datos sin pasar por la lógica de negocio.

### Flujo de datos

Como ejemplo, la búsqueda de un videojuego (CU04) sigue estos pasos:

1. El usuario escribe un título y el cliente envía un GET a `/api/juegos`.
2. El controlador recibe el pedido y lo deriva a la BLL.
3. La BLL valida los filtros y solicita los datos a la DAL, que ejecuta `sp_BuscarCatalogo`.
4. Si la búsqueda por título no devuelve resultados, la BLL consulta la API de Steam, guarda los juegos con `sp_UpsertJuegoDesdeApi` y repite la consulta.
5. Si Steam no responde, la API devuelve los datos locales e indica `parcial: true`.
6. El cliente recibe el JSON y actualiza el DOM sin recargar la página.

## APIs

### API de Steam

La API de la tienda de Steam es la fuente de datos del catálogo. Al principio del proyecto se eligió la RAWG API, pero dejó de responder durante el desarrollo y se reemplazó por Steam, que además coincide con el público objetivo definido para GAME RANK. El sistema se comunica con Steam desde el backend y no necesita clave de acceso.

| Endpoint | Uso |
|---|---|
| `featuredcategories` | Obtiene los juegos más vendidos, las novedades y las ofertas para la sincronización programada. |
| `storesearch` | Busca juegos por título cuando no existen en la base local. |
| `appdetails` | Obtiene la ficha completa de un juego. |

- **Sincronización programada:** se ejecuta al iniciar el sistema y luego cada 24 horas, con hasta 40 juegos destacados por ciclo.
- **Sincronización por búsqueda:** consulta Steam y obtiene las fichas en paralelo, con un tiempo total máximo de 1,5 segundos para respetar el requerimiento de 2 segundos.
- **Normalización:** el backend convierte la respuesta de Steam a un formato propio: quita el HTML de la sinopsis, convierte la fecha (por ejemplo, `18 MAY 2015`) y descarta lo que no es un juego, como DLC o bandas sonoras. Luego `sp_UpsertJuegoDesdeApi` guarda juegos, géneros, plataformas y desarrolladoras, y actualiza los registros existentes por su identificador de Steam.
- **Límite de peticiones:** ante un error 429, la sincronización programada espera de forma incremental y reintenta hasta tres veces.
- **Fallas:** si Steam no responde o excede el tiempo de espera, el sistema continúa con los datos locales. Cada sincronización queda registrada en `SincronizacionApi`. Para trabajar sin conexión, el sistema puede usar un cliente simulado con datos guardados de 21 juegos.

Con Steam real, la sincronización inicial incorporó 36 juegos en 10 segundos, y las búsquedas de títulos que no estaban en la base respondieron en 0,5 segundos.

**Limitaciones:**

- Los endpoints de la tienda no tienen documentación oficial y Valve podría cambiarlos.
- Steam informa solo las plataformas Windows, macOS y Linux, por lo que el filtro por plataforma (CU05) es un filtro por sistema operativo.
- Steam no ofrece un puntaje propio de los juegos. Se muestra el puntaje de Metacritic, cuando existe, y el promedio de los usuarios de GAME RANK.
- Los destacados de Steam también incluyen software, por lo que aparecen géneros como "Producción de audio".

### Cambios en el modelo de datos

El diagrama entidad-relación y el diccionario de datos deben actualizarse:

| Tabla | Cambio |
|---|---|
| Juego | `RawgId` pasa a llamarse `SteamAppId`. Se elimina `RatingRawg`. |
| Genero | `RawgId` pasa a llamarse `SteamId`. |
| Plataforma | Se elimina `RawgId`. Se identifica por `Slug`. |
| Desarrolladora | Se elimina `RawgId`. `Slug` pasa a ser UNIQUE. |

### API propia

La API propia expone endpoints REST con respuestas en JSON para autenticación, catálogo, calificaciones, reseñas, listas, perfil, moderación, administración y respaldo. El detalle se encuentra en `backend/README.md`.

La autenticación utiliza un token aleatorio de 256 bits enviado en la cabecera `Authorization`. La base de datos guarda solo su hash SHA-256, con una vigencia de 24 horas.

## Base de datos: actualización

A los procedimientos descriptos en el modelo de datos se agregaron los siguientes:

- **Catálogo y sincronización:** `sp_ListarGeneros`, `sp_ListarPlataformas`, `sp_IniciarSincronizacion` y `sp_FinalizarSincronizacion`.
- **Biblioteca y estado del usuario:** `sp_ListarEstadosLista`, `sp_ObtenerEstadoUsuarioJuego` y `sp_ListarBiblioteca`.
- **Administración:** `sp_ListarModeracion`, `sp_ListarUsuarios`, `sp_ListarRoles`, `sp_ListarAuditoria` y `sp_ObtenerPasswordHash`.
- **Respaldo:** `sp_IniciarRespaldo`, `sp_EjecutarBackup`, `sp_FinalizarRespaldo`, `sp_ListarRespaldos`, `sp_ReponerRespaldo` y `sp_RegistrarRestauracion`.

### Simulación de grandes volúmenes

El rendimiento se midió con `06_carga_masiva.sql`, sobre 50.043 juegos, 20.006 usuarios, 999.523 calificaciones, 100.006 reseñas y 299.965 registros de biblioteca. Las mediciones se hicieron con la API y SQL Server en la misma máquina, en modo Debug, sin contar la primera ejecución de cada consulta.

| Consulta | Tiempo |
|---|---|
| Catálogo sin filtros | 521 ms |
| Catálogo por puntuación | 234 ms |
| Búsqueda por título | 64 ms |
| Búsqueda con texto, géneros, plataforma y puntuación | 71 ms |
| Catálogo, página 1500 | 533 ms |
| Ficha técnica | 2 ms |
| Feed de un juego | 2 ms |
| Reseñas recientes de todos los juegos | 685 ms |
| Calificar un juego | 2 ms |
| Perfil de usuario | 3 ms |
| Moderación, reseñas recientes | 642 ms |

La consulta más lenta, las reseñas recientes en la página 500, alcanzó 749 ms, dentro del límite de 2 segundos del requerimiento no funcional.

## Calidad de software

- **Separación de responsabilidades:** el código se divide en capas con dependencias controladas.
- **Validaciones:** la BLL valida todos los datos de entrada y la base de datos los refuerza con restricciones PK, FK, UNIQUE y CHECK.
- **Errores:** todos los errores llegan al cliente con el mismo formato y el código HTTP correspondiente. Los errores no previstos se registran en el servidor sin exponer detalles.
- **Seguridad:** las contraseñas se guardan con BCrypt, las consultas se ejecutan con parámetros, y los roles se controlan en la API y en los procedimientos almacenados. Las reseñas se sanitizan antes de guardarse.
- **Trazabilidad:** toda acción de moderación, cambio de rol, respaldo y restauración queda registrada en `Auditoria` con su responsable.
- **Pruebas:** se verificaron los flujos principales y alternativos de los casos de uso contra la API en ejecución, con 406 casos de prueba automatizados que se encuentran en `backend/pruebas`.

## RNF05: información de privacidad y consentimiento explícito

El requisito de enlace al aviso y consentimiento explícito está implementado. Estos controles no acreditan por sí solos cumplimiento integral de la Ley 25.326.

- **Consentimiento:** el registro exige aceptar la Política de Privacidad. La base guarda la aceptación, la fecha y la versión de la política; los nuevos registros usan la versión 1.1, sin modificar consentimientos históricos.
- **Minimización:** el perfil público no muestra el correo. Las exportaciones CSV excluyen correos, contraseñas, sesiones y auditoría.
- **Seguridad de los datos:** las contraseñas y los tokens no se almacenan en texto plano.
- **Edición:** el usuario puede consultar su cuenta y editar nombre y avatar; el cambio de correo y las preferencias de visibilidad todavía no están disponibles.
- **Baja:** `POST /api/auth/baja` comprueba la contraseña y elimina la cuenta, sesiones, reseñas, calificaciones, biblioteca y reportes propios, salvo que existan referencias en `Auditoria.UsuarioActorId` o `Respaldo.UsuarioId`. El bloqueo también afecta a cuentas que antes tuvieron permisos administrativos; cambiar el rol no elimina esas referencias. No se borran el historial administrativo ni las copias de seguridad, que pueden conservar referencias y fragmentos de contenido.

**Pendientes:**

- Definir la conservación y atribución histórica de auditorías y respaldos antes de resolver la baja de cuentas vinculadas. No se ha aprobado eliminar historial, cambiar referencias ni anonimizar cuentas.
- Definir y publicar la identidad del responsable, un contacto de privacidad verificado y los criterios de conservación y atención de solicitudes. El aviso canónico está en `Gamerank_v3/reviews.html#privacidad`; no ofrece un correo ficticio.

## Estrategia de backup

### Qué se respalda

Se respalda la base de datos completa: usuarios, catálogo, calificaciones, reseñas, listas, auditoría e historial. Además, se puede generar una exportación CSV de los datos no sensibles.

### Cada cuánto se respaldaría

Los respaldos se generan a pedido del Super Administrador. La frecuencia propuesta para producción es la siguiente:

| Tipo | Frecuencia | Motivo |
|---|---|---|
| Completo | Semanal | Punto base de recuperación |
| Diferencial | Diaria | Reduce el tiempo de restauración |
| Log de transacciones | Cada hora | Limita la pérdida de datos a una hora y controla el crecimiento del log |

**Pendiente de revisar:** esta frecuencia es una propuesta. El sistema no programa los respaldos automáticamente.

### Cómo se recupera la información

1. El Super Administrador elige un respaldo del historial y confirma la operación.
2. El sistema verifica la integridad con SHA-256 y `RESTORE VERIFYONLY`. Si el archivo está dañado o falta, cancela la operación sin modificar la base.
3. Genera un respaldo de seguridad del estado actual.
4. Restaura la cadena necesaria: el completo, el diferencial y los logs que correspondan.
5. Si la restauración falla, vuelve al respaldo de seguridad.
6. Repone el historial de respaldos, registra la operación en la auditoría y genera un respaldo completo como nueva base.

Los datos generados después del respaldo elegido se pierden, salvo los del historial de respaldos. Se pueden recuperar restaurando el respaldo de seguridad.

Con el volumen de la carga masiva, el respaldo completo tardó 0,7 segundos (147 MB), la exportación CSV 3,9 segundos (13,7 MB) y la restauración completa 2,2 segundos.

## Chatbot

No se implementó el chatbot opcional.
