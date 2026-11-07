# Secciones para el documento técnico

Texto para sumar al documento técnico en Google Docs. Los puntos marcados como pendientes deben revisarse antes de entregar.

## Arquitectura

GAME RANK utiliza una arquitectura cliente-servidor. El cliente es una aplicación web en HTML, CSS y JavaScript que se comunica mediante fetch con una API REST propia, desarrollada en ASP.NET Core sobre .NET 10. La API accede a una base de datos Microsoft SQL Server y a la RAWG API como fuente externa de datos.

### Capas del backend

El backend se organiza en una solución con cinco proyectos:

| Capa | Responsabilidad |
|---|---|
| BE (Business Entities) | Define las entidades que comparten las demás capas. |
| Services | Reúne los servicios transversales: excepciones, hash de contraseñas, tokens, exportación CSV, manejo de archivos y cliente de la RAWG API. |
| DAL (Data Access Layer) | Accede a la base de datos con ADO.NET, por medio de procedimientos almacenados. |
| BLL (Business Logic Layer) | Aplica las reglas de negocio y coordina el acceso a datos y a los servicios. |
| API | Expone los endpoints, autentica los pedidos y traduce los errores a códigos HTTP. |

La API depende de BLL, BE y Services, pero no de DAL. Esta restricción se aplica desde la configuración del proyecto, por lo que un controlador no puede acceder a los datos sin pasar por la lógica de negocio.

### Flujo de datos

Como ejemplo, la búsqueda de un videojuego (CU04) sigue estos pasos:

1. El usuario escribe un título y el cliente envía un GET a `/api/juegos`.
2. El controlador recibe el pedido y lo deriva a la BLL.
3. La BLL valida los filtros y solicita los datos a la DAL, que ejecuta `sp_BuscarCatalogo`.
4. Si la búsqueda por título no devuelve resultados, la BLL consulta la RAWG API, guarda los juegos con `sp_UpsertJuegoDesdeApi` y repite la consulta.
5. Si la RAWG API no responde, la API devuelve los datos locales e indica `parcial: true`.
6. El cliente recibe el JSON y actualiza el DOM sin recargar la página.

## APIs

### RAWG API

La RAWG API es la fuente de datos del catálogo. El sistema se comunica con ella desde el backend, de modo que la clave de acceso no queda expuesta en el navegador.

- **Sincronización programada:** se ejecuta al iniciar el sistema y luego cada 24 horas.
- **Sincronización por búsqueda:** se ejecuta cuando un título buscado no existe en la base local.
- **Normalización:** `sp_UpsertJuegoDesdeApi` separa el JSON recibido en juegos, géneros, plataformas y desarrolladoras, y actualiza los registros existentes por su identificador externo.
- **Límite de peticiones:** ante un error 429, la sincronización programada espera de forma incremental y reintenta hasta tres veces.
- **Fallas:** si la API no responde o excede el tiempo de espera, el sistema continúa con los datos locales. Cada sincronización queda registrada en `SincronizacionApi`.

**Pendiente de revisar:** durante el desarrollo no se contó con una clave de RAWG, por lo que el sistema funciona con un cliente simulado que usa datos de ejemplo con el formato de la API. El cliente real está implementado y se probó contra un servidor local que simula respuestas de RAWG, pero nunca se conectó a la API real. Conviene aclararlo en la entrega o conseguir una clave y probarlo antes.

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

El rendimiento se midió con `06_carga_masiva.sql`, sobre 50.007 juegos, 20.006 usuarios, 999.513 calificaciones, 100.006 reseñas y 299.970 registros de biblioteca. Las mediciones se hicieron con la API y SQL Server en la misma máquina, en modo Debug, sin contar la primera ejecución de cada consulta.

| Consulta | Tiempo |
|---|---|
| Catálogo sin filtros | 509 ms |
| Catálogo por puntuación | 212 ms |
| Búsqueda por título | 60 ms |
| Búsqueda con texto, géneros, plataforma y puntuación | 91 ms |
| Catálogo, página 1500 | 335 ms |
| Ficha técnica | 2 ms |
| Feed de un juego | 3 ms |
| Calificar un juego | 15 ms |
| Perfil de usuario | 3 ms |
| Moderación, reseñas recientes | 611 ms |

La consulta más lenta alcanzó 664 ms, dentro del límite de 2 segundos del requerimiento no funcional.

## Calidad de software

- **Separación de responsabilidades:** el código se divide en capas con dependencias controladas.
- **Validaciones:** la BLL valida todos los datos de entrada y la base de datos los refuerza con restricciones PK, FK, UNIQUE y CHECK.
- **Errores:** todos los errores llegan al cliente con el mismo formato y el código HTTP correspondiente. Los errores no previstos se registran en el servidor sin exponer detalles.
- **Seguridad:** las contraseñas se guardan con BCrypt, las consultas se ejecutan con parámetros, y los roles se controlan en la API y en los procedimientos almacenados. Las reseñas se sanitizan antes de guardarse.
- **Trazabilidad:** toda acción de moderación, cambio de rol, respaldo y restauración queda registrada en `Auditoria` con su responsable.
- **Pruebas:** se verificaron los flujos principales y alternativos de los casos de uso contra la API en ejecución, con 370 casos de prueba.

## Normativa: Ley 25.326

- **Consentimiento:** el registro exige aceptar la Política de Privacidad. La base guarda la aceptación, la fecha y la versión de la política.
- **Minimización:** el perfil público no muestra el correo. Las exportaciones CSV excluyen correos, contraseñas, sesiones y auditoría.
- **Seguridad de los datos:** las contraseñas y los tokens no se almacenan en texto plano.
- **Supresión:** el usuario puede eliminar su cuenta y sus datos con `POST /api/auth/baja`.

**Pendientes de revisar:**

- Las cuentas con registros de auditoría, como las de administradores, no se pueden eliminar directamente. Debe aclararse en la Política de Privacidad o resolverse anonimizando la cuenta.
- El sistema todavía no permite rectificar el correo o el nombre de usuario. El aviso de privacidad ofrece hacerlo por correo electrónico.
- La página de Política de Privacidad corresponde al front.

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
