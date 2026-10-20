/* =============================================================================
   GAME RANK - Script 05: Datos iniciales y de prueba
   ============================================================================= */

USE GameRank;
GO

SET NOCOUNT ON;

/* ---------------------------- Catalogos fijos ---------------------------- */

INSERT INTO dbo.Rol (RolId, Nombre, Descripcion) VALUES
    (1, N'Jugador',             N'Usuario autenticado: califica, resena y gestiona sus listas'),
    (2, N'Administrador',       N'Modera resenas y bloquea/desbloquea cuentas'),
    (3, N'Super Administrador', N'Gestiona roles, auditoria, respaldo y restauracion');

INSERT INTO dbo.EstadoLista (EstadoListaId, Nombre) VALUES
    (1, N'Jugado'),
    (2, N'Quiero jugarlo'),
    (3, N'Abandonado');
GO

/* ------------------- Catalogo de ejemplo (ids reales de RAWG) ------------------- */

SET IDENTITY_INSERT dbo.Genero ON;
INSERT INTO dbo.Genero (GeneroId, RawgId, Nombre, Slug) VALUES
    (1,  4, N'Action',    N'action'),
    (2,  3, N'Adventure', N'adventure'),
    (3,  5, N'RPG',       N'role-playing-games-rpg'),
    (4,  2, N'Shooter',   N'shooter'),
    (5, 51, N'Indie',     N'indie'),
    (6,  7, N'Puzzle',    N'puzzle'),
    (7, 10, N'Strategy',  N'strategy'),
    (8, 83, N'Platformer',N'platformer');
SET IDENTITY_INSERT dbo.Genero OFF;

SET IDENTITY_INSERT dbo.Plataforma ON;
INSERT INTO dbo.Plataforma (PlataformaId, RawgId, Nombre, Slug) VALUES
    (1,   4, N'PC',              N'pc'),
    (2, 187, N'PlayStation 5',   N'playstation5'),
    (3,  18, N'PlayStation 4',   N'playstation4'),
    (4, 186, N'Xbox Series S/X', N'xbox-series-x'),
    (5,   1, N'Xbox One',        N'xbox-one'),
    (6,   7, N'Nintendo Switch', N'nintendo-switch');
SET IDENTITY_INSERT dbo.Plataforma OFF;

SET IDENTITY_INSERT dbo.Desarrolladora ON;
INSERT INTO dbo.Desarrolladora (DesarrolladoraId, RawgId, Nombre, Slug) VALUES
    (1, 9023,  N'CD PROJEKT RED',   N'cd-projekt-red'),
    (2, 3524,  N'Rockstar North',   N'rockstar-north'),
    (3, 1612,  N'Valve Software',   N'valve-software'),
    (4, 7414,  N'FromSoftware',     N'fromsoftware'),
    (5, 4004,  N'Supergiant Games', N'supergiant-games'),
    (6, 6763,  N'Team Cherry',      N'team-cherry');
SET IDENTITY_INSERT dbo.Desarrolladora OFF;

SET IDENTITY_INSERT dbo.Juego ON;
INSERT INTO dbo.Juego (JuegoId, RawgId, Titulo, Slug, Sinopsis, FechaLanzamiento, PortadaUrl, RatingRawg, Metacritic) VALUES
    (1,   3328, N'The Witcher 3: Wild Hunt',     N'the-witcher-3-wild-hunt',
        N'RPG de mundo abierto protagonizado por Geralt de Rivia.', '2015-05-18',
        N'https://media.rawg.io/media/games/618/618c2031a07bbff6b4f611f10b6bcdbc.jpg', 4.65, 92),
    (2,   3498, N'Grand Theft Auto V',           N'grand-theft-auto-v',
        N'Accion en mundo abierto en la ciudad de Los Santos.', '2013-09-17',
        N'https://media.rawg.io/media/games/20a/20aa03a10cda45239fe22d035c0ebe64.jpg', 4.47, 92),
    (3,   4200, N'Portal 2',                     N'portal-2',
        N'Puzzles en primera persona con el arma de portales.', '2011-04-18',
        N'https://media.rawg.io/media/games/2ba/2bac0e87cf45e5b508f227d281c9252a.jpg', 4.61, 95),
    (4,  58175, N'God of War',                   N'god-of-war-2',
        NULL, '2018-04-20', NULL, 4.56, 94),                             -- datos parciales (CU06 4b)
    (5, 326243, N'Elden Ring',                   N'elden-ring',
        N'Action RPG de FromSoftware en las Tierras Intermedias.', '2022-02-25',
        N'https://media.rawg.io/media/games/5ec/5ecac5cb026ec26a56efcc546364e348.jpg', 4.39, 94),
    (6,  274755, N'Hades',                       N'hades-2',
        N'Roguelike: escapa del inframundo como Zagreo.', '2020-09-17',
        N'https://media.rawg.io/media/games/1f4/1f47a270b8f241e4676b14d39ec620f7.jpg', 4.42, 93),
    (7,   9767, N'Hollow Knight',                N'hollow-knight',
        N'Metroidvania en el reino de Hallownest.', '2017-02-24',
        N'https://media.rawg.io/media/games/4cf/4cfc6b7f1850590a4634b08bfab308ab.jpg', 4.40, 90);
SET IDENTITY_INSERT dbo.Juego OFF;

INSERT INTO dbo.JuegoGenero (JuegoId, GeneroId) VALUES
    (1,1),(1,2),(1,3), (2,1),(2,2), (3,6),(3,4), (4,1),(4,2),
    (5,1),(5,3), (6,1),(6,5),(6,3), (7,1),(7,5),(7,8);

INSERT INTO dbo.JuegoPlataforma (JuegoId, PlataformaId) VALUES
    (1,1),(1,2),(1,3),(1,4),(1,5),(1,6),
    (2,1),(2,2),(2,3),(2,4),(2,5),
    (3,1),
    (4,1),(4,3),
    (5,1),(5,2),(5,3),(5,4),(5,5),
    (6,1),(6,2),(6,3),(6,4),(6,5),(6,6),
    (7,1),(7,3),(7,5),(7,6);

INSERT INTO dbo.JuegoDesarrolladora (JuegoId, DesarrolladoraId) VALUES
    (1,1),(2,2),(3,3),(5,4),(6,5),(7,6);
GO

/* ------------------------------ Usuarios de prueba ------------------------------
   PasswordHash: valores de relleno que NO corresponden a ninguna contrasena.
   Para poder iniciar sesion, registrarse desde la app (el backend genera el hash)
   y luego promover la cuenta con dbo.sp_AsignarRol.
   --------------------------------------------------------------------------------- */

SET IDENTITY_INSERT dbo.Usuario ON;
INSERT INTO dbo.Usuario (UsuarioId, Email, NombreUsuario, PasswordHash, RolId, Bloqueado,
                         AceptaPrivacidad, FechaConsentimiento, VersionPolitica) VALUES
    (1, N'superadmin@gamerank.test', N'superadmin', N'SEED_SIN_LOGIN', 3, 0, 1, SYSUTCDATETIME(), '1.0'),
    (2, N'moderador@gamerank.test',  N'moderador',  N'SEED_SIN_LOGIN', 2, 0, 1, SYSUTCDATETIME(), '1.0'),
    (3, N'lucia@gamerank.test',      N'lucia_rpg',  N'SEED_SIN_LOGIN', 1, 0, 1, SYSUTCDATETIME(), '1.0'),
    (4, N'tomas@gamerank.test',      N'tomi_fps',   N'SEED_SIN_LOGIN', 1, 0, 1, SYSUTCDATETIME(), '1.0'),
    (5, N'sofia@gamerank.test',      N'sofi_indie', N'SEED_SIN_LOGIN', 1, 0, 1, SYSUTCDATETIME(), '1.0'),
    (6, N'spammer@gamerank.test',    N'spammer99',  N'SEED_SIN_LOGIN', 1, 1, 1, SYSUTCDATETIME(), '1.0');
SET IDENTITY_INSERT dbo.Usuario OFF;
GO

/* ------------------------------ Actividad de prueba ------------------------------ */

INSERT INTO dbo.Calificacion (UsuarioId, JuegoId, Puntuacion) VALUES
    (3,1,5),(3,5,5),(3,6,4),(3,2,3),
    (4,2,5),(4,3,4),(4,4,5),
    (5,6,5),(5,7,5),(5,3,5),(5,1,4),
    (6,1,1);

INSERT INTO dbo.Resena (UsuarioId, JuegoId, Contenido) VALUES
    (3, 1, N'La mejor narrativa de un RPG. Las misiones secundarias valen tanto como la principal.'),
    (3, 5, N'Dificil pero justo. Explorar las Tierras Intermedias es una experiencia unica.'),
    (4, 2, N'El online tiene sus problemas, pero la campana sigue siendo excelente.'),
    (5, 7, N'Un metroidvania enorme por muy poco dinero. Arte y musica impecables.'),
    (5, 6, N'Cada partida se siente distinta. El sistema de dones es adictivo.'),
    (6, 1, N'COMPRA MONEDAS BARATAS EN www.ejemplo-spam.test !!!');

INSERT INTO dbo.ReporteResena (ResenaId, UsuarioReportanteId, Motivo, Comentario) VALUES
    (6, 3, 'SPAM', N'Publicidad'),
    (6, 4, 'SPAM', NULL);

INSERT INTO dbo.BibliotecaUsuario (UsuarioId, JuegoId, EstadoListaId) VALUES
    (3,1,1),(3,5,1),(3,4,2),(3,2,3),
    (4,2,1),(4,3,1),(4,5,2),
    (5,6,1),(5,7,1),(5,1,2);

INSERT INTO dbo.Auditoria (UsuarioActorId, Accion, EntidadTipo, EntidadId, Motivo) VALUES
    (2, 'BLOQUEAR_USUARIO', 'USUARIO', 6, N'Publicacion reiterada de spam');

INSERT INTO dbo.SincronizacionApi (Origen, ParametrosConsulta, FechaFin, Estado, RegistrosInsertados, RegistrosActualizados) VALUES
    ('PROGRAMADA', N'page=1&page_size=40&ordering=-rating', SYSUTCDATETIME(), 'OK', 7, 0);
GO
