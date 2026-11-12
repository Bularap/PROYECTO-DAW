SET NOCOUNT ON;
IF DB_NAME() <> N'GameRank'
    THROW 50400, N'El script requiere la base GameRank existente.', 1;
GO

UPDATE dbo.Usuario
SET PasswordHash = N'$2a$11$92Cdrfb4.lalSjIHtpgiYetiRPgWSRJbAsHka/F5cDXON8/g1XZ/q'
WHERE Email IN (N'superadmin@gamerank.test', N'moderador@gamerank.test', N'lucia@gamerank.test',
                N'tomas@gamerank.test', N'sofia@gamerank.test', N'spammer@gamerank.test')
  AND PasswordHash = N'SEED_SIN_LOGIN';

SELECT @@ROWCOUNT AS UsuariosActualizados;
GO
