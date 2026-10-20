/* =============================================================================
   GAME RANK - Script 03: Vistas
   ============================================================================= */

USE GameRank;
GO

-- Metricas agregadas por juego (ficha tecnica, tarjetas del catalogo)
CREATE OR ALTER VIEW dbo.vw_JuegoMetricas
AS
SELECT
    j.JuegoId,
    CAST(AVG(CAST(c.Puntuacion AS DECIMAL(4,2))) AS DECIMAL(3,2)) AS PromedioCalificacion,
    COUNT(c.Puntuacion)                                           AS CantidadCalificaciones
FROM dbo.Juego j
LEFT JOIN dbo.Calificacion c ON c.JuegoId = j.JuegoId
GROUP BY j.JuegoId;
GO

-- Feed de comunidad: solo resenas visibles de autores no bloqueados (CU12)
CREATE OR ALTER VIEW dbo.vw_FeedResenas
AS
SELECT
    r.ResenaId,
    r.JuegoId,
    r.UsuarioId,
    u.NombreUsuario,
    u.AvatarUrl,
    c.Puntuacion,
    r.Contenido,
    r.FechaPublicacion,
    r.FechaModificacion,
    CAST(CASE WHEN r.FechaModificacion IS NULL THEN 0 ELSE 1 END AS BIT) AS Editada
FROM dbo.Resena r
JOIN dbo.Usuario u            ON u.UsuarioId = r.UsuarioId
LEFT JOIN dbo.Calificacion c  ON c.UsuarioId = r.UsuarioId AND c.JuegoId = r.JuegoId
WHERE r.Estado = 'V'
  AND u.Bloqueado = 0;
GO

-- Panel de moderacion: resenas con reportes pendientes (CU13)
CREATE OR ALTER VIEW dbo.vw_ResenasReportadas
AS
SELECT
    r.ResenaId,
    r.JuegoId,
    j.Titulo,
    r.UsuarioId,
    u.NombreUsuario,
    r.Contenido,
    r.Estado,
    r.FechaPublicacion,
    COUNT(rr.ReporteId)  AS CantidadReportes,
    MAX(rr.FechaReporte) AS UltimoReporte
FROM dbo.Resena r
JOIN dbo.Usuario u        ON u.UsuarioId = r.UsuarioId
JOIN dbo.Juego j          ON j.JuegoId = r.JuegoId
JOIN dbo.ReporteResena rr ON rr.ResenaId = r.ResenaId AND rr.Resuelto = 0
GROUP BY r.ResenaId, r.JuegoId, j.Titulo, r.UsuarioId, u.NombreUsuario,
         r.Contenido, r.Estado, r.FechaPublicacion;
GO
