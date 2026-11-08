SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'GameRank'
    THROW 50400, N'La migración requiere la base GameRank existente.', 1;
IF OBJECT_ID(N'dbo.Usuario', N'U') IS NULL OR OBJECT_ID(N'dbo.Sesion', N'U') IS NULL
    THROW 50400, N'Faltan las tablas de cuenta esperadas.', 1;
IF ISNULL(COL_LENGTH(N'dbo.Usuario', N'PasswordHash'), -1) <> 510
    OR ISNULL(COL_LENGTH(N'dbo.Usuario', N'NombreUsuario'), -1) <> 60
    OR ISNULL(COL_LENGTH(N'dbo.Usuario', N'AvatarUrl'), -1) <> 1000
    THROW 50400, N'La estructura de Usuario no coincide con la esperada.', 1;
IF OBJECT_ID(N'dbo.sp_CrearSesion', N'P') IS NULL
    THROW 50400, N'Falta el procedimiento de sesión esperado.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Usuario') AND name = N'UQ_Usuario_NombreUsuario')
    THROW 50400, N'Falta la restricción de nombre único.', 1;
BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_CrearSesion
    @UsuarioId INT,
    @TokenHash CHAR(64),
    @PasswordHashEsperado NVARCHAR(255),
    @MinutosVida INT = 1440,
    @IpOrigen VARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @HashActual NVARCHAR(255), @Bloqueado BIT;
        SELECT @HashActual = PasswordHash, @Bloqueado = Bloqueado
        FROM dbo.Usuario WITH (UPDLOCK, HOLDLOCK)
        WHERE UsuarioId = @UsuarioId;
        IF @HashActual IS NULL OR @PasswordHashEsperado IS NULL
            OR CONVERT(VARBINARY(510), @HashActual) <> CONVERT(VARBINARY(510), @PasswordHashEsperado)
            THROW 50401, N'Correo o contraseña incorrectos.', 1;
        IF @Bloqueado = 1
            THROW 50403, N'La cuenta se encuentra suspendida.', 1;
        INSERT INTO dbo.Sesion (UsuarioId, TokenHash, FechaExpiracion, IpOrigen)
        VALUES (@UsuarioId, @TokenHash, DATEADD(MINUTE, @MinutosVida, SYSUTCDATETIME()), @IpOrigen);
        UPDATE dbo.Usuario SET UltimoLogin = SYSUTCDATETIME() WHERE UsuarioId = @UsuarioId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerCuenta
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UsuarioId, NombreUsuario, Email, AvatarUrl
    FROM dbo.Usuario
    WHERE UsuarioId = @UsuarioId AND Bloqueado = 0;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ActualizarPerfil
    @UsuarioId INT,
    @NombreUsuario NVARCHAR(30),
    @AvatarUrl NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WITH (UPDLOCK, HOLDLOCK) WHERE UsuarioId = @UsuarioId AND Bloqueado = 0)
            THROW 50401, N'La cuenta ya no está disponible.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Usuario WHERE NombreUsuario = @NombreUsuario AND UsuarioId <> @UsuarioId)
            THROW 50409, N'El nombre de usuario ya se encuentra en uso.', 1;
        UPDATE dbo.Usuario SET NombreUsuario = @NombreUsuario, AvatarUrl = @AvatarUrl
        WHERE UsuarioId = @UsuarioId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_CambiarPassword
    @UsuarioId INT,
    @PasswordHashEsperado NVARCHAR(255),
    @PasswordHashNuevo NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @HashActual NVARCHAR(255), @Bloqueado BIT;
        SELECT @HashActual = PasswordHash, @Bloqueado = Bloqueado
        FROM dbo.Usuario WITH (UPDLOCK, HOLDLOCK)
        WHERE UsuarioId = @UsuarioId;
        IF @HashActual IS NULL OR @Bloqueado = 1
            THROW 50401, N'La cuenta ya no está disponible.', 1;
        IF @PasswordHashEsperado IS NULL OR @PasswordHashNuevo IS NULL OR LEN(@PasswordHashNuevo) = 0
            THROW 50400, N'La solicitud no es válida.', 1;
        IF CONVERT(VARBINARY(510), @HashActual) <> CONVERT(VARBINARY(510), @PasswordHashEsperado)
            THROW 50409, N'Las credenciales cambiaron. Vuelva a iniciar sesión.', 1;
        UPDATE dbo.Usuario SET PasswordHash = @PasswordHashNuevo WHERE UsuarioId = @UsuarioId;
        UPDATE dbo.Sesion SET FechaRevocacion = SYSUTCDATETIME()
        WHERE UsuarioId = @UsuarioId AND FechaRevocacion IS NULL;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END
GO

COMMIT TRANSACTION;
GO
