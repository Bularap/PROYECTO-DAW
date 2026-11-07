/* =============================================================================
   GAME RANK - Script 04: Procedimientos almacenados

   Convencion de errores (THROW): el numero de error es 50000 + codigo HTTP que el
   backend debe devolver. Ej.: 50409 -> HTTP 409 Conflict, 50403 -> HTTP 403.
   Roles: 1 = Jugador, 2 = Administrador, 3 = Super Administrador
   ============================================================================= */

USE GameRank;
GO

/* -----------------------------------------------------------------------------
   M01 - Autenticacion y Seguridad
   ----------------------------------------------------------------------------- */

-- CU01: Registro. El backend envia la contrasena YA hasheada.
CREATE OR ALTER PROCEDURE dbo.sp_RegistrarUsuario
    @Email            NVARCHAR(255),
    @NombreUsuario    NVARCHAR(30),
    @PasswordHash     NVARCHAR(255),
    @AceptaPrivacidad BIT,
    @VersionPolitica  VARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;

    IF @AceptaPrivacidad IS NULL OR @AceptaPrivacidad = 0
        THROW 50400, N'Debe aceptar la Política de Privacidad (Ley 25.326).', 1;

    IF EXISTS (SELECT 1 FROM dbo.Usuario WHERE Email = @Email)
        THROW 50409, N'El correo ya se encuentra registrado.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Usuario WHERE NombreUsuario = @NombreUsuario)
        THROW 50409, N'El nombre de usuario ya se encuentra en uso.', 1;

    INSERT INTO dbo.Usuario (Email, NombreUsuario, PasswordHash, RolId,
                             AceptaPrivacidad, FechaConsentimiento, VersionPolitica)
    VALUES (@Email, @NombreUsuario, @PasswordHash, 1,
            @AceptaPrivacidad, SYSUTCDATETIME(), @VersionPolitica);

    SELECT UsuarioId, Email, NombreUsuario, RolId, FechaRegistro
    FROM dbo.Usuario
    WHERE UsuarioId = SCOPE_IDENTITY();
END
GO

-- CU02: el backend recupera el hash y lo compara del lado de la aplicacion.
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerCredenciales
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT UsuarioId, NombreUsuario, PasswordHash, RolId, Bloqueado
    FROM dbo.Usuario
    WHERE Email = @Email;
END
GO

-- CU02: se llama luego de verificar el hash. Guarda el SHA-256 del token, no el token.
CREATE OR ALTER PROCEDURE dbo.sp_CrearSesion
    @UsuarioId    INT,
    @TokenHash    CHAR(64),
    @MinutosVida  INT = 1440,
    @IpOrigen     VARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Usuario WHERE UsuarioId = @UsuarioId AND Bloqueado = 1)
        THROW 50403, N'La cuenta se encuentra suspendida.', 1;

    INSERT INTO dbo.Sesion (UsuarioId, TokenHash, FechaExpiracion, IpOrigen)
    VALUES (@UsuarioId, @TokenHash, DATEADD(MINUTE, @MinutosVida, SYSUTCDATETIME()), @IpOrigen);

    UPDATE dbo.Usuario SET UltimoLogin = SYSUTCDATETIME() WHERE UsuarioId = @UsuarioId;
END
GO

-- Middleware de autenticacion: devuelve el usuario si el token es valido.
CREATE OR ALTER PROCEDURE dbo.sp_ValidarSesion
    @TokenHash CHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT u.UsuarioId, u.NombreUsuario, u.RolId, s.FechaExpiracion
    FROM dbo.Sesion s
    JOIN dbo.Usuario u ON u.UsuarioId = s.UsuarioId
    WHERE s.TokenHash = @TokenHash
      AND s.FechaRevocacion IS NULL
      AND s.FechaExpiracion > SYSUTCDATETIME()
      AND u.Bloqueado = 0;
END
GO

-- CU03: Cerrar sesion. Idempotente (3a: token ya expirado no es error).
CREATE OR ALTER PROCEDURE dbo.sp_CerrarSesion
    @TokenHash CHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Sesion
    SET FechaRevocacion = SYSUTCDATETIME()
    WHERE TokenHash = @TokenHash
      AND FechaRevocacion IS NULL;
END
GO

/* -----------------------------------------------------------------------------
   M02 - Catalogo y Busqueda
   ----------------------------------------------------------------------------- */

-- CU04 + CU05: busqueda por titulo y filtros combinables (listas de slugs separadas por coma).
-- Dentro de una misma lista basta con coincidir con uno; entre criterios se exigen todos.
CREATE OR ALTER PROCEDURE dbo.sp_BuscarCatalogo
    @Texto          NVARCHAR(100) = NULL,
    @Generos        NVARCHAR(500) = NULL,   -- ej: 'action,role-playing-games-rpg'
    @Plataformas    NVARCHAR(500) = NULL,   -- ej: 'pc,playstation5'
    @PuntuacionMin  DECIMAL(3,2)  = NULL,
    @PuntuacionMax  DECIMAL(3,2)  = NULL,
    @Orden          VARCHAR(15)   = 'TITULO', -- TITULO | PUNTUACION | LANZAMIENTO
    @Pagina         INT           = 1,
    @TamanioPagina  INT           = 24
AS
BEGIN
    SET NOCOUNT ON;

    IF @Pagina < 1 SET @Pagina = 1;
    IF @TamanioPagina NOT BETWEEN 1 AND 100 SET @TamanioPagina = 24;

    WITH Base AS (
        SELECT j.JuegoId, j.SteamAppId, j.Titulo, j.Slug, j.PortadaUrl, j.FechaLanzamiento,
               m.PromedioCalificacion, m.CantidadCalificaciones
        FROM dbo.Juego j
        OUTER APPLY (
            SELECT CAST(AVG(CAST(c.Puntuacion AS DECIMAL(4,2))) AS DECIMAL(3,2)) AS PromedioCalificacion,
                   COUNT(*) AS CantidadCalificaciones
            FROM dbo.Calificacion c
            WHERE c.JuegoId = j.JuegoId
        ) m
        WHERE (@Texto IS NULL OR j.Titulo LIKE N'%' + @Texto + N'%')
          AND (@Generos IS NULL OR EXISTS (
                SELECT 1
                FROM dbo.JuegoGenero jg
                JOIN dbo.Genero g ON g.GeneroId = jg.GeneroId
                WHERE jg.JuegoId = j.JuegoId
                  AND g.Slug IN (SELECT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@Generos, ','))))
          AND (@Plataformas IS NULL OR EXISTS (
                SELECT 1
                FROM dbo.JuegoPlataforma jp
                JOIN dbo.Plataforma p ON p.PlataformaId = jp.PlataformaId
                WHERE jp.JuegoId = j.JuegoId
                  AND p.Slug IN (SELECT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@Plataformas, ','))))
    ),
    Pagina AS (
        SELECT JuegoId, SteamAppId, Titulo, Slug, PortadaUrl, FechaLanzamiento,
               PromedioCalificacion, CantidadCalificaciones,
               COUNT(*) OVER () AS TotalResultados
        FROM Base
        WHERE (@PuntuacionMin IS NULL OR PromedioCalificacion >= @PuntuacionMin)
          AND (@PuntuacionMax IS NULL OR PromedioCalificacion <= @PuntuacionMax)
        ORDER BY
            CASE WHEN @Orden = 'PUNTUACION'  THEN PromedioCalificacion END DESC,
            CASE WHEN @Orden = 'LANZAMIENTO' THEN FechaLanzamiento END DESC,
            Titulo, JuegoId
        OFFSET (@Pagina - 1) * @TamanioPagina ROWS
        FETCH NEXT @TamanioPagina ROWS ONLY
    )
    SELECT p.JuegoId, p.SteamAppId, p.Titulo, p.Slug, p.PortadaUrl, p.FechaLanzamiento,
           p.PromedioCalificacion, p.CantidadCalificaciones, p.TotalResultados,
           (SELECT g.GeneroId AS id, g.Nombre AS nombre, g.Slug AS slug
            FROM dbo.JuegoGenero jg
            JOIN dbo.Genero g ON g.GeneroId = jg.GeneroId
            WHERE jg.JuegoId = p.JuegoId
            ORDER BY g.Nombre
            FOR JSON PATH) AS Generos
    FROM Pagina p
    ORDER BY
        CASE WHEN @Orden = 'PUNTUACION'  THEN p.PromedioCalificacion END DESC,
        CASE WHEN @Orden = 'LANZAMIENTO' THEN p.FechaLanzamiento END DESC,
        p.Titulo, p.JuegoId
    OPTION (RECOMPILE);
END
GO

-- CU06: ficha tecnica. Result sets: 1) juego + metricas, 2) generos, 3) plataformas, 4) desarrolladoras.
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerFichaJuego
    @JuegoId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Juego WHERE JuegoId = @JuegoId)
        THROW 50404, N'Juego no encontrado.', 1;

    SELECT j.JuegoId, j.SteamAppId, j.Titulo, j.Slug, j.Sinopsis, j.FechaLanzamiento,
           j.PortadaUrl, j.Metacritic, j.FechaSincronizacion,
           m.PromedioCalificacion, m.CantidadCalificaciones
    FROM dbo.Juego j
    JOIN dbo.vw_JuegoMetricas m ON m.JuegoId = j.JuegoId
    WHERE j.JuegoId = @JuegoId;

    SELECT g.GeneroId, g.Nombre, g.Slug
    FROM dbo.JuegoGenero jg JOIN dbo.Genero g ON g.GeneroId = jg.GeneroId
    WHERE jg.JuegoId = @JuegoId
    ORDER BY g.Nombre;

    SELECT p.PlataformaId, p.Nombre, p.Slug
    FROM dbo.JuegoPlataforma jp JOIN dbo.Plataforma p ON p.PlataformaId = jp.PlataformaId
    WHERE jp.JuegoId = @JuegoId
    ORDER BY p.Nombre;

    SELECT d.DesarrolladoraId, d.Nombre, d.Slug
    FROM dbo.JuegoDesarrolladora jd JOIN dbo.Desarrolladora d ON d.DesarrolladoraId = jd.DesarrolladoraId
    WHERE jd.JuegoId = @JuegoId
    ORDER BY d.Nombre;
END
GO

/* -----------------------------------------------------------------------------
   M03 - Integracion API Externa (CU15)
   Recibe el JSON normalizado de un juego de Steam y lo guarda en Juego, Genero,
   Plataforma y Desarrolladora. Las relaciones se reemplazan solo si vienen en el JSON.
   ----------------------------------------------------------------------------- */

CREATE OR ALTER PROCEDURE dbo.sp_UpsertJuegoDesdeApi
    @Json      NVARCHAR(MAX),
    @Resultado CHAR(1) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF ISJSON(@Json) <> 1
        THROW 50400, N'El JSON recibido no es válido.', 1;

    DECLARE @SteamAppId INT, @Titulo NVARCHAR(255), @Slug NVARCHAR(255), @Sinopsis NVARCHAR(MAX),
            @Lanzamiento DATE, @Portada NVARCHAR(500), @Metacritic TINYINT;

    SELECT @SteamAppId = SteamAppId, @Titulo = Titulo, @Slug = Slug, @Sinopsis = Sinopsis,
           @Lanzamiento = Lanzamiento, @Portada = Portada, @Metacritic = Metacritic
    FROM OPENJSON(@Json) WITH (
        SteamAppId  INT            '$.steamAppId',
        Titulo      NVARCHAR(255)  '$.nombre',
        Slug        NVARCHAR(255)  '$.slug',
        Sinopsis    NVARCHAR(MAX)  '$.sinopsis',
        Lanzamiento DATE           '$.lanzamiento',
        Portada     NVARCHAR(500)  '$.portada',
        Metacritic  TINYINT        '$.metacritic'
    );

    IF @SteamAppId IS NULL OR @Titulo IS NULL OR @Slug IS NULL
        THROW 50400, N'El JSON no contiene steamAppId, nombre o slug.', 1;

    DECLARE @JuegoId INT;

    BEGIN TRANSACTION;

    UPDATE dbo.Juego WITH (UPDLOCK, SERIALIZABLE)
    SET @JuegoId = JuegoId,
        Titulo = @Titulo, Slug = @Slug,
        Sinopsis = COALESCE(@Sinopsis, Sinopsis),
        FechaLanzamiento = COALESCE(@Lanzamiento, FechaLanzamiento),
        PortadaUrl = COALESCE(@Portada, PortadaUrl),
        Metacritic = COALESCE(@Metacritic, Metacritic),
        FechaSincronizacion = SYSUTCDATETIME()
    WHERE SteamAppId = @SteamAppId;

    IF @JuegoId IS NULL
    BEGIN
        INSERT INTO dbo.Juego (SteamAppId, Titulo, Slug, Sinopsis, FechaLanzamiento, PortadaUrl, Metacritic)
        VALUES (@SteamAppId, @Titulo, @Slug, @Sinopsis, @Lanzamiento, @Portada, @Metacritic);
        SET @JuegoId = SCOPE_IDENTITY();
        SET @Resultado = 'I';
    END
    ELSE
        SET @Resultado = 'U';

    IF JSON_QUERY(@Json, '$.generos') IS NOT NULL
    BEGIN
        MERGE dbo.Genero AS t
        USING (SELECT id, MAX(nombre) AS nombre, MAX(slug) AS slug
               FROM OPENJSON(@Json, '$.generos') WITH (id INT, nombre NVARCHAR(60), slug NVARCHAR(60))
               GROUP BY id) AS s
        ON t.SteamId = s.id
        WHEN MATCHED AND t.Nombre <> s.nombre THEN UPDATE SET Nombre = s.nombre
        WHEN NOT MATCHED THEN INSERT (SteamId, Nombre, Slug) VALUES (s.id, s.nombre, s.slug);

        DELETE FROM dbo.JuegoGenero WHERE JuegoId = @JuegoId;
        INSERT INTO dbo.JuegoGenero (JuegoId, GeneroId)
        SELECT DISTINCT @JuegoId, g.GeneroId
        FROM OPENJSON(@Json, '$.generos') WITH (id INT) s
        JOIN dbo.Genero g ON g.SteamId = s.id;
    END

    IF JSON_QUERY(@Json, '$.plataformas') IS NOT NULL
    BEGIN
        MERGE dbo.Plataforma AS t
        USING (SELECT slug, MAX(nombre) AS nombre
               FROM OPENJSON(@Json, '$.plataformas') WITH (nombre NVARCHAR(60), slug NVARCHAR(60))
               GROUP BY slug) AS s
        ON t.Slug = s.slug
        WHEN NOT MATCHED THEN INSERT (Nombre, Slug) VALUES (s.nombre, s.slug);

        DELETE FROM dbo.JuegoPlataforma WHERE JuegoId = @JuegoId;
        INSERT INTO dbo.JuegoPlataforma (JuegoId, PlataformaId)
        SELECT DISTINCT @JuegoId, p.PlataformaId
        FROM OPENJSON(@Json, '$.plataformas') WITH (slug NVARCHAR(60)) s
        JOIN dbo.Plataforma p ON p.Slug = s.slug;
    END

    IF JSON_QUERY(@Json, '$.desarrolladoras') IS NOT NULL
    BEGIN
        MERGE dbo.Desarrolladora AS t
        USING (SELECT slug, MAX(nombre) AS nombre
               FROM OPENJSON(@Json, '$.desarrolladoras') WITH (nombre NVARCHAR(150), slug NVARCHAR(150))
               GROUP BY slug) AS s
        ON t.Slug = s.slug
        WHEN NOT MATCHED THEN INSERT (Nombre, Slug) VALUES (s.nombre, s.slug);

        DELETE FROM dbo.JuegoDesarrolladora WHERE JuegoId = @JuegoId;
        INSERT INTO dbo.JuegoDesarrolladora (JuegoId, DesarrolladoraId)
        SELECT DISTINCT @JuegoId, d.DesarrolladoraId
        FROM OPENJSON(@Json, '$.desarrolladoras') WITH (slug NVARCHAR(150)) s
        JOIN dbo.Desarrolladora d ON d.Slug = s.slug;
    END

    COMMIT TRANSACTION;

    SELECT @JuegoId AS JuegoId, @Resultado AS Resultado;
END
GO

/* -----------------------------------------------------------------------------
   M04 - Valoracion y Resenas
   ----------------------------------------------------------------------------- */

-- CU07: alta o modificacion de la calificacion; devuelve el nuevo promedio.
CREATE OR ALTER PROCEDURE dbo.sp_CalificarJuego
    @UsuarioId  INT,
    @JuegoId    INT,
    @Puntuacion TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Puntuacion NOT BETWEEN 1 AND 5
        THROW 50400, N'La puntuación debe estar entre 1 y 5.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Juego WHERE JuegoId = @JuegoId)
        THROW 50404, N'Juego no encontrado.', 1;

    BEGIN TRANSACTION;

    UPDATE dbo.Calificacion WITH (UPDLOCK, SERIALIZABLE)
    SET Puntuacion = @Puntuacion, FechaModificacion = SYSUTCDATETIME()
    WHERE UsuarioId = @UsuarioId AND JuegoId = @JuegoId;

    IF @@ROWCOUNT = 0
        INSERT INTO dbo.Calificacion (UsuarioId, JuegoId, Puntuacion)
        VALUES (@UsuarioId, @JuegoId, @Puntuacion);

    COMMIT TRANSACTION;

    SELECT PromedioCalificacion, CantidadCalificaciones
    FROM dbo.vw_JuegoMetricas
    WHERE JuegoId = @JuegoId;
END
GO

-- CU07 1a: retiro de la calificacion.
CREATE OR ALTER PROCEDURE dbo.sp_EliminarCalificacion
    @UsuarioId INT,
    @JuegoId   INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Calificacion WHERE UsuarioId = @UsuarioId AND JuegoId = @JuegoId;

    SELECT PromedioCalificacion, CantidadCalificaciones
    FROM dbo.vw_JuegoMetricas
    WHERE JuegoId = @JuegoId;
END
GO

-- CU08: redactar resena. El backend sanitiza el texto antes de llamar.
CREATE OR ALTER PROCEDURE dbo.sp_CrearResena
    @UsuarioId INT,
    @JuegoId   INT,
    @Contenido NVARCHAR(2000)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Juego WHERE JuegoId = @JuegoId)
        THROW 50404, N'Juego no encontrado.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Resena WHERE UsuarioId = @UsuarioId AND JuegoId = @JuegoId)
        THROW 50409, N'Ya publicaste una reseña para este juego.', 1;

    INSERT INTO dbo.Resena (UsuarioId, JuegoId, Contenido)
    VALUES (@UsuarioId, @JuegoId, @Contenido);

    SELECT * FROM dbo.vw_FeedResenas WHERE ResenaId = SCOPE_IDENTITY();
END
GO

-- CU09: editar resena propia.
CREATE OR ALTER PROCEDURE dbo.sp_EditarResena
    @UsuarioId INT,
    @ResenaId  INT,
    @Contenido NVARCHAR(2000)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Autor INT = (SELECT UsuarioId FROM dbo.Resena WHERE ResenaId = @ResenaId AND Estado = 'V');

    IF @Autor IS NULL
        THROW 50404, N'La reseña no existe.', 1;
    IF @Autor <> @UsuarioId
        THROW 50403, N'Solo el autor puede modificar la reseña.', 1;

    UPDATE dbo.Resena
    SET Contenido = @Contenido, FechaModificacion = SYSUTCDATETIME()
    WHERE ResenaId = @ResenaId;

    SELECT * FROM dbo.vw_FeedResenas WHERE ResenaId = @ResenaId;
END
GO

-- CU09 3a: eliminar resena propia.
CREATE OR ALTER PROCEDURE dbo.sp_EliminarResena
    @UsuarioId INT,
    @ResenaId  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Autor INT = (SELECT UsuarioId FROM dbo.Resena WHERE ResenaId = @ResenaId AND Estado = 'V');

    IF @Autor IS NULL
        THROW 50404, N'La reseña no existe.', 1;
    IF @Autor <> @UsuarioId
        THROW 50403, N'Solo el autor puede eliminar la reseña.', 1;

    DELETE FROM dbo.Resena WHERE ResenaId = @ResenaId;
END
GO

-- Reportar una resena para que la revise un administrador.
CREATE OR ALTER PROCEDURE dbo.sp_ReportarResena
    @UsuarioId  INT,
    @ResenaId   INT,
    @Motivo     VARCHAR(20),
    @Comentario NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Resena WHERE ResenaId = @ResenaId AND Estado = 'V')
        THROW 50404, N'La reseña no existe.', 1;

    IF EXISTS (SELECT 1 FROM dbo.ReporteResena WHERE ResenaId = @ResenaId AND UsuarioReportanteId = @UsuarioId)
        THROW 50409, N'Ya reportaste esta reseña.', 1;

    INSERT INTO dbo.ReporteResena (ResenaId, UsuarioReportanteId, Motivo, Comentario)
    VALUES (@ResenaId, @UsuarioId, @Motivo, @Comentario);
END
GO

/* -----------------------------------------------------------------------------
   M05 - Biblioteca Personal (CU10)
   ----------------------------------------------------------------------------- */

CREATE OR ALTER PROCEDURE dbo.sp_AsignarEstadoBiblioteca
    @UsuarioId     INT,
    @JuegoId       INT,
    @EstadoListaId TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.EstadoLista WHERE EstadoListaId = @EstadoListaId)
        THROW 50400, N'Estado de lista no válido.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Juego WHERE JuegoId = @JuegoId)
        THROW 50404, N'Juego no encontrado.', 1;

    BEGIN TRANSACTION;

    UPDATE dbo.BibliotecaUsuario WITH (UPDLOCK, SERIALIZABLE)
    SET EstadoListaId = @EstadoListaId, FechaModificacion = SYSUTCDATETIME()
    WHERE UsuarioId = @UsuarioId AND JuegoId = @JuegoId;

    IF @@ROWCOUNT = 0
        INSERT INTO dbo.BibliotecaUsuario (UsuarioId, JuegoId, EstadoListaId)
        VALUES (@UsuarioId, @JuegoId, @EstadoListaId);

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_QuitarDeBiblioteca
    @UsuarioId INT,
    @JuegoId   INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.BibliotecaUsuario WHERE UsuarioId = @UsuarioId AND JuegoId = @JuegoId;
END
GO

/* -----------------------------------------------------------------------------
   M06 - Perfil y Comunidad
   ----------------------------------------------------------------------------- */

-- CU11. Result sets: 1) datos + metricas, 2) cantidad por estado, 3) historial de calificaciones, 4) resenas.
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerPerfil
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE UsuarioId = @UsuarioId AND Bloqueado = 0)
        THROW 50404, N'Perfil no disponible.', 1;

    SELECT u.UsuarioId, u.NombreUsuario, u.AvatarUrl, u.FechaRegistro,
           (SELECT CAST(AVG(CAST(Puntuacion AS DECIMAL(4,2))) AS DECIMAL(3,2))
              FROM dbo.Calificacion WHERE UsuarioId = u.UsuarioId)         AS PromedioOtorgado,
           (SELECT COUNT(*) FROM dbo.Calificacion WHERE UsuarioId = u.UsuarioId) AS CantidadCalificaciones,
           (SELECT COUNT(*) FROM dbo.Resena WHERE UsuarioId = u.UsuarioId AND Estado = 'V') AS CantidadResenas
    FROM dbo.Usuario u
    WHERE u.UsuarioId = @UsuarioId;

    SELECT e.EstadoListaId, e.Nombre, COUNT(b.JuegoId) AS Cantidad
    FROM dbo.EstadoLista e
    LEFT JOIN dbo.BibliotecaUsuario b ON b.EstadoListaId = e.EstadoListaId AND b.UsuarioId = @UsuarioId
    GROUP BY e.EstadoListaId, e.Nombre
    ORDER BY e.EstadoListaId;

    SELECT j.JuegoId, j.Titulo, j.PortadaUrl, c.Puntuacion,
           COALESCE(c.FechaModificacion, c.FechaCalificacion) AS Fecha
    FROM dbo.Calificacion c
    JOIN dbo.Juego j ON j.JuegoId = c.JuegoId
    WHERE c.UsuarioId = @UsuarioId
    ORDER BY Fecha DESC;

    SELECT f.ResenaId, f.JuegoId, j.Titulo, f.Puntuacion, f.Contenido,
           f.FechaPublicacion, f.Editada
    FROM dbo.vw_FeedResenas f
    JOIN dbo.Juego j ON j.JuegoId = f.JuegoId
    WHERE f.UsuarioId = @UsuarioId
    ORDER BY f.FechaPublicacion DESC;
END
GO

-- CU12: feed paginado de resenas de un juego.
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerFeedJuego
    @JuegoId       INT,
    @Pagina        INT = 1,
    @TamanioPagina INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    IF @Pagina < 1 SET @Pagina = 1;
    IF @TamanioPagina NOT BETWEEN 1 AND 50 SET @TamanioPagina = 10;

    SELECT f.ResenaId, f.JuegoId, j.Titulo, f.UsuarioId, f.NombreUsuario, f.AvatarUrl, f.Puntuacion, f.Contenido,
           f.FechaPublicacion, f.Editada,
           COUNT(*) OVER () AS TotalResenas
    FROM dbo.vw_FeedResenas f
    JOIN dbo.Juego j ON j.JuegoId = f.JuegoId
    WHERE f.JuegoId = @JuegoId
    ORDER BY f.FechaPublicacion DESC, f.ResenaId DESC
    OFFSET (@Pagina - 1) * @TamanioPagina ROWS
    FETCH NEXT @TamanioPagina ROWS ONLY;
END
GO

/* -----------------------------------------------------------------------------
   M07 - Administracion y Moderacion
   ----------------------------------------------------------------------------- */

-- CU13: ocultar / mostrar / eliminar una resena (requiere rol >= Administrador).
CREATE OR ALTER PROCEDURE dbo.sp_ModerarResena
    @AdminId  INT,
    @ResenaId INT,
    @Accion   VARCHAR(10),        -- OCULTAR | MOSTRAR | ELIMINAR
    @Motivo   NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE UsuarioId = @AdminId AND RolId >= 2 AND Bloqueado = 0)
        THROW 50403, N'Rol insuficiente.', 1;

    IF @Accion NOT IN ('OCULTAR', 'MOSTRAR', 'ELIMINAR')
        THROW 50400, N'Acción de moderación no válida.', 1;

    DECLARE @Detalle NVARCHAR(1000);
    SELECT @Detalle = CONCAT(N'Autor=', UsuarioId, N'; Juego=', JuegoId, N'; Texto=', LEFT(Contenido, 200))
    FROM dbo.Resena WHERE ResenaId = @ResenaId;

    IF @Detalle IS NULL
        THROW 50404, N'La reseña no existe.', 1;

    BEGIN TRANSACTION;

    IF @Accion = 'ELIMINAR'
        DELETE FROM dbo.Resena WHERE ResenaId = @ResenaId;   -- los reportes se eliminan en cascada
    ELSE
    BEGIN
        UPDATE dbo.Resena
        SET Estado = CASE @Accion WHEN 'OCULTAR' THEN 'O' ELSE 'V' END
        WHERE ResenaId = @ResenaId;

        UPDATE dbo.ReporteResena SET Resuelto = 1 WHERE ResenaId = @ResenaId AND Resuelto = 0;
    END

    INSERT INTO dbo.Auditoria (UsuarioActorId, Accion, EntidadTipo, EntidadId, Motivo, Detalle)
    VALUES (@AdminId, @Accion + '_RESENA', 'RESENA', @ResenaId, @Motivo, @Detalle);

    COMMIT TRANSACTION;
END
GO

-- CU13 5a / 5b: bloquear o desbloquear una cuenta.
CREATE OR ALTER PROCEDURE dbo.sp_CambiarBloqueoUsuario
    @AdminId   INT,
    @UsuarioId INT,
    @Bloquear  BIT,
    @Motivo    NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @RolAdmin TINYINT = (SELECT RolId FROM dbo.Usuario WHERE UsuarioId = @AdminId AND Bloqueado = 0);
    DECLARE @RolObjetivo TINYINT = (SELECT RolId FROM dbo.Usuario WHERE UsuarioId = @UsuarioId);

    IF @RolAdmin IS NULL OR @RolAdmin < 2
        THROW 50403, N'Rol insuficiente.', 1;
    IF @RolObjetivo IS NULL
        THROW 50404, N'Usuario no encontrado.', 1;
    IF @AdminId = @UsuarioId OR @RolObjetivo >= @RolAdmin
        THROW 50403, N'No puede bloquear a un usuario con rol igual o superior.', 1;

    BEGIN TRANSACTION;

    UPDATE dbo.Usuario SET Bloqueado = @Bloquear WHERE UsuarioId = @UsuarioId;

    -- Un bloqueo invalida las sesiones activas
    IF @Bloquear = 1
        UPDATE dbo.Sesion SET FechaRevocacion = SYSUTCDATETIME()
        WHERE UsuarioId = @UsuarioId AND FechaRevocacion IS NULL;

    INSERT INTO dbo.Auditoria (UsuarioActorId, Accion, EntidadTipo, EntidadId, Motivo)
    VALUES (@AdminId, CASE WHEN @Bloquear = 1 THEN 'BLOQUEAR_USUARIO' ELSE 'DESBLOQUEAR_USUARIO' END,
            'USUARIO', @UsuarioId, @Motivo);

    COMMIT TRANSACTION;
END
GO

-- CU14: asignar rol (solo Super Administrador; no puede autodegradarse).
CREATE OR ALTER PROCEDURE dbo.sp_AsignarRol
    @SuperAdminId INT,
    @UsuarioId    INT,
    @RolId        TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE UsuarioId = @SuperAdminId AND RolId = 3 AND Bloqueado = 0)
        THROW 50403, N'Rol insuficiente.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Rol WHERE RolId = @RolId)
        THROW 50400, N'El rol indicado no existe.', 1;
    IF @SuperAdminId = @UsuarioId AND @RolId <> 3
        THROW 50400, N'No puede revocarse su propio rol de Super Administrador.', 1;

    DECLARE @RolAnterior TINYINT = (SELECT RolId FROM dbo.Usuario WHERE UsuarioId = @UsuarioId);
    IF @RolAnterior IS NULL
        THROW 50404, N'Usuario no encontrado.', 1;

    BEGIN TRANSACTION;

    UPDATE dbo.Usuario SET RolId = @RolId WHERE UsuarioId = @UsuarioId;

    INSERT INTO dbo.Auditoria (UsuarioActorId, Accion, EntidadTipo, EntidadId, Detalle)
    VALUES (@SuperAdminId, 'CAMBIAR_ROL', 'USUARIO', @UsuarioId,
            CONCAT(N'RolAnterior=', @RolAnterior, N'; RolNuevo=', @RolId));

    COMMIT TRANSACTION;
END
GO

-- Ley 25.326 (derecho de supresion): baja definitiva de la cuenta y sus datos.
CREATE OR ALTER PROCEDURE dbo.sp_EliminarUsuario
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM dbo.Auditoria WHERE UsuarioActorId = @UsuarioId)
       OR EXISTS (SELECT 1 FROM dbo.Respaldo WHERE UsuarioId = @UsuarioId)
        THROW 50409, N'La cuenta tiene registros de auditoría; quite primero su rol administrativo.', 1;

    BEGIN TRANSACTION;

    -- FK sin cascada (ver 02_tables.sql)
    DELETE FROM dbo.ReporteResena WHERE UsuarioReportanteId = @UsuarioId;
    -- Sesion, Calificacion, Resena (y sus reportes) y BibliotecaUsuario se borran en cascada
    DELETE FROM dbo.Usuario WHERE UsuarioId = @UsuarioId;

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarGeneros
AS
BEGIN
    SET NOCOUNT ON;

    SELECT GeneroId AS Id, Nombre, Slug
    FROM dbo.Genero
    ORDER BY Nombre;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarPlataformas
AS
BEGIN
    SET NOCOUNT ON;

    SELECT PlataformaId AS Id, Nombre, Slug
    FROM dbo.Plataforma
    ORDER BY Nombre;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_IniciarSincronizacion
    @Origen             VARCHAR(20),
    @ParametrosConsulta NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.SincronizacionApi (Origen, ParametrosConsulta)
    VALUES (@Origen, @ParametrosConsulta);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS SincronizacionId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_FinalizarSincronizacion
    @SincronizacionId INT,
    @Estado           VARCHAR(15),
    @Insertados       INT,
    @Actualizados     INT,
    @ConError         INT,
    @Reintentos       TINYINT,
    @MensajeError     NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.SincronizacionApi
    SET FechaFin = SYSUTCDATETIME(),
        Estado = @Estado,
        RegistrosInsertados = @Insertados,
        RegistrosActualizados = @Actualizados,
        RegistrosConError = @ConError,
        Reintentos = @Reintentos,
        MensajeError = @MensajeError
    WHERE SincronizacionId = @SincronizacionId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarEstadosLista
AS
BEGIN
    SET NOCOUNT ON;

    SELECT EstadoListaId, Nombre
    FROM dbo.EstadoLista
    ORDER BY EstadoListaId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerEstadoUsuarioJuego
    @UsuarioId INT,
    @JuegoId   INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Juego WHERE JuegoId = @JuegoId)
        THROW 50404, N'Juego no encontrado.', 1;

    SELECT
        (SELECT Puntuacion FROM dbo.Calificacion WHERE UsuarioId = @UsuarioId AND JuegoId = @JuegoId) AS Puntuacion,
        (SELECT EstadoListaId FROM dbo.BibliotecaUsuario WHERE UsuarioId = @UsuarioId AND JuegoId = @JuegoId) AS EstadoListaId,
        (SELECT ResenaId FROM dbo.Resena WHERE UsuarioId = @UsuarioId AND JuegoId = @JuegoId AND Estado = 'V') AS ResenaId,
        (SELECT Contenido FROM dbo.Resena WHERE UsuarioId = @UsuarioId AND JuegoId = @JuegoId AND Estado = 'V') AS ResenaContenido;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarBiblioteca
    @UsuarioId     INT,
    @EstadoListaId TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE UsuarioId = @UsuarioId AND Bloqueado = 0)
        THROW 50404, N'Perfil no disponible.', 1;

    SELECT b.JuegoId, j.Titulo, j.PortadaUrl, b.EstadoListaId, e.Nombre AS Estado,
           COALESCE(b.FechaModificacion, b.FechaAgregado) AS Fecha
    FROM dbo.BibliotecaUsuario b
    JOIN dbo.Juego j ON j.JuegoId = b.JuegoId
    JOIN dbo.EstadoLista e ON e.EstadoListaId = b.EstadoListaId
    WHERE b.UsuarioId = @UsuarioId
      AND (@EstadoListaId IS NULL OR b.EstadoListaId = @EstadoListaId)
    ORDER BY Fecha DESC, j.Titulo;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarModeracion
    @Filtro        VARCHAR(12),
    @Pagina        INT,
    @TamanioPagina INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT r.ResenaId, r.JuegoId, j.Titulo, r.UsuarioId, u.NombreUsuario,
           u.Bloqueado AS AutorBloqueado, r.Contenido, r.Estado, r.FechaPublicacion,
           p.CantidadReportes, p.UltimoReporte, p.Motivos,
           COUNT(*) OVER () AS Total
    FROM dbo.Resena r
    JOIN dbo.Usuario u ON u.UsuarioId = r.UsuarioId
    JOIN dbo.Juego j ON j.JuegoId = r.JuegoId
    OUTER APPLY (
        SELECT COUNT(*) AS CantidadReportes,
               MAX(rr.FechaReporte) AS UltimoReporte,
               STRING_AGG(rr.Motivo, ', ') AS Motivos
        FROM dbo.ReporteResena rr
        WHERE rr.ResenaId = r.ResenaId AND rr.Resuelto = 0
    ) p
    WHERE @Filtro = 'RECIENTES'
       OR (@Filtro = 'REPORTADAS' AND p.CantidadReportes > 0)
       OR (@Filtro = 'OCULTAS' AND r.Estado = 'O')
    ORDER BY
        CASE WHEN @Filtro = 'REPORTADAS' THEN p.CantidadReportes END DESC,
        r.FechaPublicacion DESC,
        r.ResenaId DESC
    OFFSET (@Pagina - 1) * @TamanioPagina ROWS
    FETCH NEXT @TamanioPagina ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarUsuarios
    @Texto          NVARCHAR(100) = NULL,
    @SoloBloqueados BIT = 0,
    @Pagina         INT,
    @TamanioPagina  INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT u.UsuarioId, u.NombreUsuario, u.Email, u.RolId, u.Bloqueado, u.FechaRegistro, u.UltimoLogin,
           COUNT(*) OVER () AS Total
    FROM dbo.Usuario u
    WHERE (@SoloBloqueados = 0 OR u.Bloqueado = 1)
      AND (@Texto IS NULL
           OR u.NombreUsuario LIKE N'%' + @Texto + N'%'
           OR u.Email LIKE N'%' + @Texto + N'%')
    ORDER BY u.NombreUsuario
    OFFSET (@Pagina - 1) * @TamanioPagina ROWS
    FETCH NEXT @TamanioPagina ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarRoles
AS
BEGIN
    SET NOCOUNT ON;

    SELECT RolId, Nombre, Descripcion
    FROM dbo.Rol
    ORDER BY RolId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarAuditoria
    @Pagina        INT,
    @TamanioPagina INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT a.AuditoriaId, a.UsuarioActorId, u.NombreUsuario AS Actor, a.Accion, a.EntidadTipo,
           a.EntidadId, a.Motivo, a.Detalle, a.Fecha,
           COUNT(*) OVER () AS Total
    FROM dbo.Auditoria a
    JOIN dbo.Usuario u ON u.UsuarioId = a.UsuarioActorId
    ORDER BY a.Fecha DESC, a.AuditoriaId DESC
    OFFSET (@Pagina - 1) * @TamanioPagina ROWS
    FETCH NEXT @TamanioPagina ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerPasswordHash
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT PasswordHash
    FROM dbo.Usuario
    WHERE UsuarioId = @UsuarioId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_IniciarRespaldo
    @UsuarioId   INT,
    @Tipo        VARCHAR(15),
    @Formato     VARCHAR(5),
    @RutaArchivo NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Responsable INT = COALESCE(
        (SELECT UsuarioId FROM dbo.Usuario WHERE UsuarioId = @UsuarioId),
        (SELECT TOP 1 UsuarioId FROM dbo.Usuario WHERE RolId = 3 ORDER BY UsuarioId));

    INSERT INTO dbo.Respaldo (UsuarioId, Tipo, Formato, RutaArchivo)
    VALUES (@Responsable, @Tipo, @Formato, @RutaArchivo);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS RespaldoId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_EjecutarBackup
    @Tipo VARCHAR(15),
    @Ruta NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Base SYSNAME = DB_NAME();

    IF @Tipo = 'COMPLETO'
        BACKUP DATABASE @Base TO DISK = @Ruta WITH INIT, CHECKSUM;
    ELSE IF @Tipo = 'DIFERENCIAL'
        BACKUP DATABASE @Base TO DISK = @Ruta WITH DIFFERENTIAL, INIT, CHECKSUM;
    ELSE IF @Tipo = 'LOG'
        BACKUP LOG @Base TO DISK = @Ruta WITH INIT, CHECKSUM;
    ELSE
        THROW 50400, N'El tipo de respaldo no es válido.', 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_FinalizarRespaldo
    @RespaldoId     INT,
    @Estado         VARCHAR(15),
    @TamanioBytes   BIGINT = NULL,
    @ChecksumSha256 CHAR(64) = NULL,
    @MensajeError   NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE dbo.Respaldo
    SET Estado = @Estado,
        TamanioBytes = @TamanioBytes,
        ChecksumSha256 = @ChecksumSha256,
        MensajeError = @MensajeError,
        FechaFin = SYSUTCDATETIME()
    WHERE RespaldoId = @RespaldoId;

    IF @Estado = 'OK'
        INSERT INTO dbo.Auditoria (UsuarioActorId, Accion, EntidadTipo, EntidadId, Detalle)
        SELECT UsuarioId, 'GENERAR_RESPALDO', 'RESPALDO', RespaldoId, CONCAT(N'Tipo=', Tipo, N'; Formato=', Formato)
        FROM dbo.Respaldo
        WHERE RespaldoId = @RespaldoId;

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarRespaldos
    @Pagina        INT,
    @TamanioPagina INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT r.RespaldoId, r.UsuarioId, u.NombreUsuario, r.Tipo, r.Formato, r.RutaArchivo, r.TamanioBytes,
           r.ChecksumSha256, r.Estado, r.FechaInicio, r.FechaFin, r.MensajeError,
           COUNT(*) OVER () AS Total
    FROM dbo.Respaldo r
    JOIN dbo.Usuario u ON u.UsuarioId = r.UsuarioId
    ORDER BY r.RespaldoId DESC
    OFFSET (@Pagina - 1) * @TamanioPagina ROWS
    FETCH NEXT @TamanioPagina ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ReponerRespaldo
    @RespaldoId     INT,
    @UsuarioId      INT,
    @Tipo           VARCHAR(15),
    @Formato        VARCHAR(5),
    @RutaArchivo    NVARCHAR(500),
    @TamanioBytes   BIGINT = NULL,
    @ChecksumSha256 CHAR(64) = NULL,
    @Estado         VARCHAR(15),
    @FechaInicio    DATETIME2(0),
    @FechaFin       DATETIME2(0) = NULL,
    @MensajeError   NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Responsable INT = COALESCE(
        (SELECT UsuarioId FROM dbo.Usuario WHERE UsuarioId = @UsuarioId),
        (SELECT TOP 1 UsuarioId FROM dbo.Usuario WHERE RolId = 3 ORDER BY UsuarioId));

    IF EXISTS (SELECT 1 FROM dbo.Respaldo WHERE RespaldoId = @RespaldoId)
        UPDATE dbo.Respaldo
        SET UsuarioId = @Responsable, Tipo = @Tipo, Formato = @Formato, RutaArchivo = @RutaArchivo,
            TamanioBytes = @TamanioBytes, ChecksumSha256 = @ChecksumSha256, Estado = @Estado,
            FechaInicio = @FechaInicio, FechaFin = @FechaFin, MensajeError = @MensajeError
        WHERE RespaldoId = @RespaldoId;
    ELSE
    BEGIN
        SET IDENTITY_INSERT dbo.Respaldo ON;

        INSERT INTO dbo.Respaldo (RespaldoId, UsuarioId, Tipo, Formato, RutaArchivo, TamanioBytes, ChecksumSha256,
                                  Estado, FechaInicio, FechaFin, MensajeError)
        VALUES (@RespaldoId, @Responsable, @Tipo, @Formato, @RutaArchivo, @TamanioBytes, @ChecksumSha256,
                @Estado, @FechaInicio, @FechaFin, @MensajeError);

        SET IDENTITY_INSERT dbo.Respaldo OFF;
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarRestauracion
    @UsuarioId  INT,
    @RespaldoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Responsable INT = COALESCE(
        (SELECT UsuarioId FROM dbo.Usuario WHERE UsuarioId = @UsuarioId),
        (SELECT TOP 1 UsuarioId FROM dbo.Usuario WHERE RolId = 3 ORDER BY UsuarioId));

    BEGIN TRANSACTION;

    UPDATE dbo.Respaldo SET Estado = 'RESTAURADO' WHERE RespaldoId = @RespaldoId;

    INSERT INTO dbo.Auditoria (UsuarioActorId, Accion, EntidadTipo, EntidadId, Detalle)
    VALUES (@Responsable, 'RESTAURAR_RESPALDO', 'RESPALDO', @RespaldoId, CONCAT(N'SolicitadoPor=', @UsuarioId));

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerResenasRecientes
    @Pagina        INT,
    @TamanioPagina INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT f.ResenaId, f.JuegoId, j.Titulo, f.UsuarioId, f.NombreUsuario, f.AvatarUrl, f.Puntuacion, f.Contenido,
           f.FechaPublicacion, f.Editada,
           COUNT(*) OVER () AS TotalResenas
    FROM dbo.vw_FeedResenas f
    JOIN dbo.Juego j ON j.JuegoId = f.JuegoId
    ORDER BY f.FechaPublicacion DESC, f.ResenaId DESC
    OFFSET (@Pagina - 1) * @TamanioPagina ROWS
    FETCH NEXT @TamanioPagina ROWS ONLY;
END
GO
