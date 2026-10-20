/* =============================================================================
   GAME RANK - Script 06: Simulacion de grandes volumenes (Big Data)
   Genera datos sinteticos para medir el rendimiento de consultas e indices.
   Ajustar las variables de cantidad segun el equipo. Con los valores por defecto
   tarda menos de un minuto en una instancia local.
   ============================================================================= */

USE GameRank;
GO

SET NOCOUNT ON;

DECLARE @CantJuegos        INT = 50000;
DECLARE @CantUsuarios      INT = 20000;
DECLARE @CantCalificaciones INT = 1000000;  -- intentos; se descartan pares usuario-juego repetidos
DECLARE @CantResenas       INT = 100000;
DECLARE @CantBiblioteca    INT = 300000;

-- Tabla de numeros 1..N
IF OBJECT_ID('tempdb..#Numeros') IS NOT NULL DROP TABLE #Numeros;
SELECT TOP (@CantCalificaciones) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
INTO #Numeros
FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c;
CREATE UNIQUE CLUSTERED INDEX IX_Numeros ON #Numeros (n);

/* ---- Juegos (RawgId >= 10.000.000 para no chocar con ids reales) ---- */
DECLARE @PrimerJuego INT = ISNULL((SELECT MAX(JuegoId) FROM dbo.Juego), 0) + 1;

INSERT INTO dbo.Juego (RawgId, Titulo, Slug, Sinopsis, FechaLanzamiento, PortadaUrl, RatingRawg, Metacritic)
SELECT 10000000 + n,
       CONCAT(N'Juego Sintetico ', n),
       CONCAT(N'juego-sintetico-', n),
       N'Registro generado para pruebas de volumen.',
       DATEADD(DAY, -(ABS(CHECKSUM(NEWID())) % 9000), CAST(GETDATE() AS DATE)),
       NULL,
       CAST((ABS(CHECKSUM(NEWID())) % 500) / 100.0 AS DECIMAL(3,2)),
       CASE WHEN n % 3 = 0 THEN NULL ELSE 40 + ABS(CHECKSUM(NEWID())) % 60 END
FROM #Numeros WHERE n <= @CantJuegos;

DECLARE @UltimoJuego INT = (SELECT MAX(JuegoId) FROM dbo.Juego);

-- 1 a 3 generos y 1 a 4 plataformas por juego
INSERT INTO dbo.JuegoGenero (JuegoId, GeneroId)
SELECT DISTINCT j.JuegoId, g.GeneroId
FROM dbo.Juego j
CROSS APPLY (SELECT TOP (1 + j.JuegoId % 3) GeneroId FROM dbo.Genero ORDER BY CHECKSUM(NEWID(), j.JuegoId)) g
WHERE j.JuegoId >= @PrimerJuego;

INSERT INTO dbo.JuegoPlataforma (JuegoId, PlataformaId)
SELECT DISTINCT j.JuegoId, p.PlataformaId
FROM dbo.Juego j
CROSS APPLY (SELECT TOP (1 + j.JuegoId % 4) PlataformaId FROM dbo.Plataforma ORDER BY CHECKSUM(NEWID(), j.JuegoId)) p
WHERE j.JuegoId >= @PrimerJuego;

/* ---- Usuarios ---- */
DECLARE @PrimerUsuario INT = ISNULL((SELECT MAX(UsuarioId) FROM dbo.Usuario), 0) + 1;

INSERT INTO dbo.Usuario (Email, NombreUsuario, PasswordHash, RolId, AceptaPrivacidad, FechaConsentimiento, VersionPolitica, FechaRegistro)
SELECT CONCAT(N'user', n, N'@carga.test'),
       CONCAT(N'user', n),
       N'SEED_SIN_LOGIN',
       1, 1, SYSUTCDATETIME(), '1.0',
       DATEADD(MINUTE, -(ABS(CHECKSUM(NEWID())) % 1000000), SYSUTCDATETIME())
FROM #Numeros WHERE n <= @CantUsuarios;

DECLARE @UltimoUsuario INT = (SELECT MAX(UsuarioId) FROM dbo.Usuario);
DECLARE @RangoUsuarios INT = @UltimoUsuario - @PrimerUsuario + 1;
DECLARE @RangoJuegos   INT = @UltimoJuego - @PrimerJuego + 1;

/* ---- Calificaciones (distribucion sesgada hacia 3-5 estrellas) ---- */
INSERT INTO dbo.Calificacion (UsuarioId, JuegoId, Puntuacion, FechaCalificacion)
SELECT UsuarioId, JuegoId, MAX(Puntuacion), MAX(Fecha)
FROM (
    SELECT @PrimerUsuario + ABS(CHECKSUM(NEWID())) % @RangoUsuarios AS UsuarioId,
           @PrimerJuego   + ABS(CHECKSUM(NEWID())) % @RangoJuegos   AS JuegoId,
           CAST(CASE WHEN ABS(CHECKSUM(NEWID())) % 10 < 7
                     THEN 3 + ABS(CHECKSUM(NEWID())) % 3
                     ELSE 1 + ABS(CHECKSUM(NEWID())) % 2 END AS TINYINT) AS Puntuacion,
           DATEADD(MINUTE, -(ABS(CHECKSUM(NEWID())) % 500000), SYSUTCDATETIME()) AS Fecha
    FROM #Numeros
) x
GROUP BY UsuarioId, JuegoId;

/* ---- Resenas: sobre un subconjunto de las calificaciones ---- */
INSERT INTO dbo.Resena (UsuarioId, JuegoId, Contenido, FechaPublicacion)
SELECT TOP (@CantResenas)
       c.UsuarioId, c.JuegoId,
       CONCAT(N'Resena de prueba generada automaticamente. Puntuacion otorgada: ', c.Puntuacion, N' estrellas.'),
       c.FechaCalificacion
FROM dbo.Calificacion c
WHERE c.UsuarioId >= @PrimerUsuario
ORDER BY NEWID();

/* ---- Biblioteca ---- */
INSERT INTO dbo.BibliotecaUsuario (UsuarioId, JuegoId, EstadoListaId)
SELECT UsuarioId, JuegoId, MAX(EstadoListaId)
FROM (
    SELECT TOP (@CantBiblioteca)
           @PrimerUsuario + ABS(CHECKSUM(NEWID())) % @RangoUsuarios AS UsuarioId,
           @PrimerJuego   + ABS(CHECKSUM(NEWID())) % @RangoJuegos   AS JuegoId,
           CAST(1 + ABS(CHECKSUM(NEWID())) % 3 AS TINYINT)          AS EstadoListaId
    FROM #Numeros
) x
GROUP BY UsuarioId, JuegoId;

DROP TABLE #Numeros;

UPDATE STATISTICS dbo.Juego;
UPDATE STATISTICS dbo.Calificacion;
UPDATE STATISTICS dbo.Resena;
UPDATE STATISTICS dbo.BibliotecaUsuario;

SELECT 'Juego' AS Tabla, COUNT(*) AS Filas FROM dbo.Juego
UNION ALL SELECT 'Usuario', COUNT(*) FROM dbo.Usuario
UNION ALL SELECT 'Calificacion', COUNT(*) FROM dbo.Calificacion
UNION ALL SELECT 'Resena', COUNT(*) FROM dbo.Resena
UNION ALL SELECT 'BibliotecaUsuario', COUNT(*) FROM dbo.BibliotecaUsuario;
GO
