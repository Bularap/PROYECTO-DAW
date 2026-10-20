/* =============================================================================
   GAME RANK - Script 02: Tablas, claves, restricciones e indices
   ============================================================================= */

USE GameRank;
GO

/* -----------------------------------------------------------------------------
   M01 - Autenticacion y Seguridad
   ----------------------------------------------------------------------------- */

CREATE TABLE dbo.Rol (
    RolId           TINYINT       NOT NULL,
    Nombre          NVARCHAR(30)  NOT NULL,
    Descripcion     NVARCHAR(200) NULL,
    CONSTRAINT PK_Rol PRIMARY KEY (RolId),
    CONSTRAINT UQ_Rol_Nombre UNIQUE (Nombre)
);
GO

CREATE TABLE dbo.Usuario (
    UsuarioId             INT IDENTITY(1,1) NOT NULL,
    Email                 NVARCHAR(255) NOT NULL,
    NombreUsuario         NVARCHAR(30)  NOT NULL,
    PasswordHash          NVARCHAR(255) NOT NULL,   -- hash generado por el backend (bcrypt/argon2/SHA2_256+salt). Nunca texto plano.
    AvatarUrl             NVARCHAR(500) NULL,
    RolId                 TINYINT       NOT NULL CONSTRAINT DF_Usuario_RolId DEFAULT (1),
    Bloqueado             BIT           NOT NULL CONSTRAINT DF_Usuario_Bloqueado DEFAULT (0),
    -- Ley 25.326: consentimiento explicito al tratamiento de datos personales
    AceptaPrivacidad      BIT           NOT NULL,
    FechaConsentimiento   DATETIME2(0)  NOT NULL,
    VersionPolitica       VARCHAR(10)   NOT NULL,
    FechaRegistro         DATETIME2(0)  NOT NULL CONSTRAINT DF_Usuario_FechaRegistro DEFAULT (SYSUTCDATETIME()),
    UltimoLogin           DATETIME2(0)  NULL,
    CONSTRAINT PK_Usuario PRIMARY KEY (UsuarioId),
    CONSTRAINT UQ_Usuario_Email UNIQUE (Email),
    CONSTRAINT UQ_Usuario_NombreUsuario UNIQUE (NombreUsuario),
    CONSTRAINT FK_Usuario_Rol FOREIGN KEY (RolId) REFERENCES dbo.Rol (RolId),
    CONSTRAINT CK_Usuario_Email CHECK (Email LIKE '%_@_%._%'),
    CONSTRAINT CK_Usuario_AceptaPrivacidad CHECK (AceptaPrivacidad = 1)
);
GO

CREATE TABLE dbo.Sesion (
    SesionId        BIGINT IDENTITY(1,1) NOT NULL,
    UsuarioId       INT           NOT NULL,
    TokenHash       CHAR(64)      NOT NULL,        -- SHA-256 en hex del token; el token real solo vive en el cliente
    FechaCreacion   DATETIME2(0)  NOT NULL CONSTRAINT DF_Sesion_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaExpiracion DATETIME2(0)  NOT NULL,
    FechaRevocacion DATETIME2(0)  NULL,            -- NULL = sesion vigente (logout la completa, CU03)
    IpOrigen        VARCHAR(45)   NULL,
    CONSTRAINT PK_Sesion PRIMARY KEY (SesionId),
    CONSTRAINT UQ_Sesion_TokenHash UNIQUE (TokenHash),
    CONSTRAINT FK_Sesion_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario (UsuarioId) ON DELETE CASCADE,
    CONSTRAINT CK_Sesion_Expiracion CHECK (FechaExpiracion > FechaCreacion)
);
GO

CREATE INDEX IX_Sesion_UsuarioId ON dbo.Sesion (UsuarioId);
GO

/* -----------------------------------------------------------------------------
   M02 / M03 - Catalogo (datos normalizados desde la RAWG API)
   ----------------------------------------------------------------------------- */

CREATE TABLE dbo.Desarrolladora (
    DesarrolladoraId INT IDENTITY(1,1) NOT NULL,
    RawgId           INT           NOT NULL,
    Nombre           NVARCHAR(150) NOT NULL,
    Slug             NVARCHAR(150) NOT NULL,
    CONSTRAINT PK_Desarrolladora PRIMARY KEY (DesarrolladoraId),
    CONSTRAINT UQ_Desarrolladora_RawgId UNIQUE (RawgId)
);
GO

CREATE TABLE dbo.Genero (
    GeneroId    INT IDENTITY(1,1) NOT NULL,
    RawgId      INT           NOT NULL,
    Nombre      NVARCHAR(60)  NOT NULL,
    Slug        NVARCHAR(60)  NOT NULL,
    CONSTRAINT PK_Genero PRIMARY KEY (GeneroId),
    CONSTRAINT UQ_Genero_RawgId UNIQUE (RawgId),
    CONSTRAINT UQ_Genero_Slug UNIQUE (Slug)
);
GO

CREATE TABLE dbo.Plataforma (
    PlataformaId INT IDENTITY(1,1) NOT NULL,
    RawgId       INT           NOT NULL,
    Nombre       NVARCHAR(60)  NOT NULL,
    Slug         NVARCHAR(60)  NOT NULL,
    CONSTRAINT PK_Plataforma PRIMARY KEY (PlataformaId),
    CONSTRAINT UQ_Plataforma_RawgId UNIQUE (RawgId),
    CONSTRAINT UQ_Plataforma_Slug UNIQUE (Slug)
);
GO

CREATE TABLE dbo.Juego (
    JuegoId           INT IDENTITY(1,1) NOT NULL,
    RawgId            INT            NOT NULL,
    Titulo            NVARCHAR(255)  NOT NULL,
    Slug              NVARCHAR(255)  NOT NULL,
    Sinopsis          NVARCHAR(MAX)  NULL,          -- campos opcionales de la API: NULL => "Informacion no disponible" (CU06 4b)
    FechaLanzamiento  DATE           NULL,
    PortadaUrl        NVARCHAR(500)  NULL,
    RatingRawg        DECIMAL(3,2)   NULL,
    Metacritic        TINYINT        NULL,
    FechaAlta         DATETIME2(0)   NOT NULL CONSTRAINT DF_Juego_FechaAlta DEFAULT (SYSUTCDATETIME()),
    FechaSincronizacion DATETIME2(0) NOT NULL CONSTRAINT DF_Juego_FechaSync DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Juego PRIMARY KEY (JuegoId),
    CONSTRAINT UQ_Juego_RawgId UNIQUE (RawgId),
    CONSTRAINT UQ_Juego_Slug UNIQUE (Slug),
    CONSTRAINT CK_Juego_RatingRawg CHECK (RatingRawg BETWEEN 0 AND 5),
    CONSTRAINT CK_Juego_Metacritic CHECK (Metacritic BETWEEN 0 AND 100)
);
GO

CREATE INDEX IX_Juego_Titulo ON dbo.Juego (Titulo) INCLUDE (PortadaUrl, FechaLanzamiento);
CREATE INDEX IX_Juego_FechaLanzamiento ON dbo.Juego (FechaLanzamiento);
GO

-- Relaciones N:M del catalogo
CREATE TABLE dbo.JuegoGenero (
    JuegoId  INT NOT NULL,
    GeneroId INT NOT NULL,
    CONSTRAINT PK_JuegoGenero PRIMARY KEY (JuegoId, GeneroId),
    CONSTRAINT FK_JuegoGenero_Juego  FOREIGN KEY (JuegoId)  REFERENCES dbo.Juego (JuegoId)   ON DELETE CASCADE,
    CONSTRAINT FK_JuegoGenero_Genero FOREIGN KEY (GeneroId) REFERENCES dbo.Genero (GeneroId) ON DELETE CASCADE
);
CREATE INDEX IX_JuegoGenero_GeneroId ON dbo.JuegoGenero (GeneroId, JuegoId);
GO

CREATE TABLE dbo.JuegoPlataforma (
    JuegoId      INT NOT NULL,
    PlataformaId INT NOT NULL,
    CONSTRAINT PK_JuegoPlataforma PRIMARY KEY (JuegoId, PlataformaId),
    CONSTRAINT FK_JuegoPlataforma_Juego      FOREIGN KEY (JuegoId)      REFERENCES dbo.Juego (JuegoId)           ON DELETE CASCADE,
    CONSTRAINT FK_JuegoPlataforma_Plataforma FOREIGN KEY (PlataformaId) REFERENCES dbo.Plataforma (PlataformaId) ON DELETE CASCADE
);
CREATE INDEX IX_JuegoPlataforma_PlataformaId ON dbo.JuegoPlataforma (PlataformaId, JuegoId);
GO

CREATE TABLE dbo.JuegoDesarrolladora (
    JuegoId          INT NOT NULL,
    DesarrolladoraId INT NOT NULL,
    CONSTRAINT PK_JuegoDesarrolladora PRIMARY KEY (JuegoId, DesarrolladoraId),
    CONSTRAINT FK_JuegoDesarrolladora_Juego FOREIGN KEY (JuegoId) REFERENCES dbo.Juego (JuegoId) ON DELETE CASCADE,
    CONSTRAINT FK_JuegoDesarrolladora_Desarrolladora FOREIGN KEY (DesarrolladoraId) REFERENCES dbo.Desarrolladora (DesarrolladoraId) ON DELETE CASCADE
);
CREATE INDEX IX_JuegoDesarrolladora_DesarrolladoraId ON dbo.JuegoDesarrolladora (DesarrolladoraId, JuegoId);
GO

/* -----------------------------------------------------------------------------
   M04 - Valoracion y Resenas
   ----------------------------------------------------------------------------- */

CREATE TABLE dbo.Calificacion (
    UsuarioId          INT          NOT NULL,
    JuegoId            INT          NOT NULL,
    Puntuacion         TINYINT      NOT NULL,
    FechaCalificacion  DATETIME2(0) NOT NULL CONSTRAINT DF_Calificacion_Fecha DEFAULT (SYSUTCDATETIME()),
    FechaModificacion  DATETIME2(0) NULL,
    -- PK compuesta = unicidad usuario-juego (CU07: se actualiza, no se duplica)
    CONSTRAINT PK_Calificacion PRIMARY KEY (UsuarioId, JuegoId),
    CONSTRAINT FK_Calificacion_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario (UsuarioId) ON DELETE CASCADE,
    CONSTRAINT FK_Calificacion_Juego   FOREIGN KEY (JuegoId)   REFERENCES dbo.Juego (JuegoId)     ON DELETE CASCADE,
    CONSTRAINT CK_Calificacion_Puntuacion CHECK (Puntuacion BETWEEN 1 AND 5)
);
GO

-- Promedio y cantidad por juego (ficha, catalogo, filtro por puntuacion)
CREATE INDEX IX_Calificacion_JuegoId ON dbo.Calificacion (JuegoId) INCLUDE (Puntuacion);
GO

CREATE TABLE dbo.Resena (
    ResenaId           INT IDENTITY(1,1) NOT NULL,
    UsuarioId          INT            NOT NULL,
    JuegoId            INT            NOT NULL,
    Contenido          NVARCHAR(2000) NOT NULL,
    Estado             CHAR(1)        NOT NULL CONSTRAINT DF_Resena_Estado DEFAULT ('V'),  -- V = Visible, O = Oculta (moderacion)
    FechaPublicacion   DATETIME2(0)   NOT NULL CONSTRAINT DF_Resena_FechaPub DEFAULT (SYSUTCDATETIME()),
    FechaModificacion  DATETIME2(0)   NULL,                                                -- NOT NULL => marca "(editada)"
    CONSTRAINT PK_Resena PRIMARY KEY (ResenaId),
    CONSTRAINT UQ_Resena_Usuario_Juego UNIQUE (UsuarioId, JuegoId),                       -- CU08 6a: una resena por usuario y juego
    CONSTRAINT FK_Resena_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario (UsuarioId) ON DELETE CASCADE,
    CONSTRAINT FK_Resena_Juego   FOREIGN KEY (JuegoId)   REFERENCES dbo.Juego (JuegoId)     ON DELETE CASCADE,
    CONSTRAINT CK_Resena_Estado CHECK (Estado IN ('V', 'O')),
    CONSTRAINT CK_Resena_Longitud CHECK (LEN(Contenido) BETWEEN 10 AND 2000)
);
GO

-- Feed de comunidad: resenas visibles de un juego, mas recientes primero (CU12)
CREATE INDEX IX_Resena_Juego_Feed ON dbo.Resena (JuegoId, Estado, FechaPublicacion DESC);
GO

CREATE TABLE dbo.ReporteResena (
    ReporteId           INT IDENTITY(1,1) NOT NULL,
    ResenaId            INT           NOT NULL,
    UsuarioReportanteId INT           NOT NULL,
    Motivo              VARCHAR(20)   NOT NULL,
    Comentario          NVARCHAR(300) NULL,
    FechaReporte        DATETIME2(0)  NOT NULL CONSTRAINT DF_ReporteResena_Fecha DEFAULT (SYSUTCDATETIME()),
    Resuelto            BIT           NOT NULL CONSTRAINT DF_ReporteResena_Resuelto DEFAULT (0),
    CONSTRAINT PK_ReporteResena PRIMARY KEY (ReporteId),
    CONSTRAINT UQ_ReporteResena UNIQUE (ResenaId, UsuarioReportanteId),
    CONSTRAINT FK_ReporteResena_Resena FOREIGN KEY (ResenaId) REFERENCES dbo.Resena (ResenaId) ON DELETE CASCADE,
    -- NO ACTION: SQL Server no admite multiples rutas de cascada (Usuario->Resena->Reporte y Usuario->Reporte).
    -- La baja de usuario se resuelve en dbo.sp_EliminarUsuario.
    CONSTRAINT FK_ReporteResena_Usuario FOREIGN KEY (UsuarioReportanteId) REFERENCES dbo.Usuario (UsuarioId),
    CONSTRAINT CK_ReporteResena_Motivo CHECK (Motivo IN ('SPAM', 'OFENSIVO', 'SPOILER', 'OTRO'))
);
GO

CREATE INDEX IX_ReporteResena_Pendientes ON dbo.ReporteResena (Resuelto, FechaReporte);
CREATE INDEX IX_ReporteResena_Usuario ON dbo.ReporteResena (UsuarioReportanteId);
GO

/* -----------------------------------------------------------------------------
   M05 - Biblioteca Personal (listas de estado / wishlist)
   ----------------------------------------------------------------------------- */

CREATE TABLE dbo.EstadoLista (
    EstadoListaId TINYINT      NOT NULL,
    Nombre        NVARCHAR(30) NOT NULL,
    CONSTRAINT PK_EstadoLista PRIMARY KEY (EstadoListaId),
    CONSTRAINT UQ_EstadoLista_Nombre UNIQUE (Nombre)
);
GO

CREATE TABLE dbo.BibliotecaUsuario (
    UsuarioId          INT          NOT NULL,
    JuegoId            INT          NOT NULL,
    EstadoListaId      TINYINT      NOT NULL,
    FechaAgregado      DATETIME2(0) NOT NULL CONSTRAINT DF_Biblioteca_FechaAgregado DEFAULT (SYSUTCDATETIME()),
    FechaModificacion  DATETIME2(0) NULL,
    -- PK compuesta = un unico estado vigente por usuario y juego (CU10)
    CONSTRAINT PK_BibliotecaUsuario PRIMARY KEY (UsuarioId, JuegoId),
    CONSTRAINT FK_Biblioteca_Usuario FOREIGN KEY (UsuarioId)     REFERENCES dbo.Usuario (UsuarioId) ON DELETE CASCADE,
    CONSTRAINT FK_Biblioteca_Juego   FOREIGN KEY (JuegoId)       REFERENCES dbo.Juego (JuegoId)     ON DELETE CASCADE,
    CONSTRAINT FK_Biblioteca_Estado  FOREIGN KEY (EstadoListaId) REFERENCES dbo.EstadoLista (EstadoListaId)
);
GO

CREATE INDEX IX_Biblioteca_Usuario_Estado ON dbo.BibliotecaUsuario (UsuarioId, EstadoListaId);
CREATE INDEX IX_Biblioteca_JuegoId ON dbo.BibliotecaUsuario (JuegoId);
GO

/* -----------------------------------------------------------------------------
   M07 - Administracion y Moderacion (auditoria)
   ----------------------------------------------------------------------------- */

CREATE TABLE dbo.Auditoria (
    AuditoriaId     BIGINT IDENTITY(1,1) NOT NULL,
    UsuarioActorId  INT           NOT NULL,            -- administrador / super administrador responsable
    Accion          VARCHAR(30)   NOT NULL,
    EntidadTipo     VARCHAR(30)   NOT NULL,
    EntidadId       INT           NOT NULL,            -- sin FK: la entidad puede haber sido eliminada
    Motivo          NVARCHAR(300) NULL,
    Detalle         NVARCHAR(1000) NULL,
    Fecha           DATETIME2(0)  NOT NULL CONSTRAINT DF_Auditoria_Fecha DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Auditoria PRIMARY KEY (AuditoriaId),
    CONSTRAINT FK_Auditoria_Usuario FOREIGN KEY (UsuarioActorId) REFERENCES dbo.Usuario (UsuarioId),
    CONSTRAINT CK_Auditoria_Accion CHECK (Accion IN (
        'OCULTAR_RESENA', 'MOSTRAR_RESENA', 'ELIMINAR_RESENA',
        'BLOQUEAR_USUARIO', 'DESBLOQUEAR_USUARIO', 'CAMBIAR_ROL',
        'GENERAR_RESPALDO', 'RESTAURAR_RESPALDO')),
    CONSTRAINT CK_Auditoria_EntidadTipo CHECK (EntidadTipo IN ('RESENA', 'USUARIO', 'RESPALDO'))
);
GO

CREATE INDEX IX_Auditoria_Fecha ON dbo.Auditoria (Fecha DESC);
CREATE INDEX IX_Auditoria_Actor ON dbo.Auditoria (UsuarioActorId);
GO

/* -----------------------------------------------------------------------------
   M03 - Bitacora de sincronizacion con la RAWG API (CU15)
   ----------------------------------------------------------------------------- */

CREATE TABLE dbo.SincronizacionApi (
    SincronizacionId      INT IDENTITY(1,1) NOT NULL,
    Origen                VARCHAR(20)    NOT NULL,     -- PROGRAMADA | BUSQUEDA
    ParametrosConsulta    NVARCHAR(500)  NULL,
    FechaInicio           DATETIME2(0)   NOT NULL CONSTRAINT DF_Sync_FechaInicio DEFAULT (SYSUTCDATETIME()),
    FechaFin              DATETIME2(0)   NULL,         -- NULL mientras esta en curso
    Estado                VARCHAR(15)    NOT NULL CONSTRAINT DF_Sync_Estado DEFAULT ('EN_CURSO'),
    RegistrosInsertados   INT            NOT NULL CONSTRAINT DF_Sync_Insertados DEFAULT (0),
    RegistrosActualizados INT            NOT NULL CONSTRAINT DF_Sync_Actualizados DEFAULT (0),
    RegistrosConError     INT            NOT NULL CONSTRAINT DF_Sync_Errores DEFAULT (0),
    Reintentos            TINYINT        NOT NULL CONSTRAINT DF_Sync_Reintentos DEFAULT (0),
    MensajeError          NVARCHAR(1000) NULL,
    CONSTRAINT PK_SincronizacionApi PRIMARY KEY (SincronizacionId),
    CONSTRAINT CK_Sync_Origen CHECK (Origen IN ('PROGRAMADA', 'BUSQUEDA')),
    CONSTRAINT CK_Sync_Estado CHECK (Estado IN ('EN_CURSO', 'OK', 'PARCIAL', 'ERROR')),
    CONSTRAINT CK_Sync_Fechas CHECK (FechaFin IS NULL OR FechaFin >= FechaInicio)
);
GO

/* -----------------------------------------------------------------------------
   M08 - Persistencia y Respaldo (CU16)
   ----------------------------------------------------------------------------- */

CREATE TABLE dbo.Respaldo (
    RespaldoId      INT IDENTITY(1,1) NOT NULL,
    UsuarioId       INT            NOT NULL,           -- super administrador que lo ejecuto
    Tipo            VARCHAR(15)    NOT NULL,
    Formato         VARCHAR(5)     NOT NULL,
    RutaArchivo     NVARCHAR(500)  NOT NULL,
    TamanioBytes    BIGINT         NULL,               -- se completa al finalizar
    ChecksumSha256  CHAR(64)       NULL,               -- validacion de integridad antes de restaurar (CU16 3b)
    Estado          VARCHAR(15)    NOT NULL CONSTRAINT DF_Respaldo_Estado DEFAULT ('EN_CURSO'),
    FechaInicio     DATETIME2(0)   NOT NULL CONSTRAINT DF_Respaldo_FechaInicio DEFAULT (SYSUTCDATETIME()),
    FechaFin        DATETIME2(0)   NULL,
    MensajeError    NVARCHAR(1000) NULL,
    CONSTRAINT PK_Respaldo PRIMARY KEY (RespaldoId),
    CONSTRAINT FK_Respaldo_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario (UsuarioId),
    CONSTRAINT CK_Respaldo_Tipo CHECK (Tipo IN ('COMPLETO', 'DIFERENCIAL', 'LOG', 'EXPORTACION')),
    CONSTRAINT CK_Respaldo_Formato CHECK (Formato IN ('BAK', 'TRN', 'CSV', 'SQL')),
    CONSTRAINT CK_Respaldo_Estado CHECK (Estado IN ('EN_CURSO', 'OK', 'ERROR', 'RESTAURADO')),
    CONSTRAINT CK_Respaldo_Tamanio CHECK (TamanioBytes IS NULL OR TamanioBytes >= 0),
    CONSTRAINT CK_Respaldo_Fechas CHECK (FechaFin IS NULL OR FechaFin >= FechaInicio)
);
GO
