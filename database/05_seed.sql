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

/* ------------------- Catalogo de ejemplo (ids reales de Steam) ------------------- */

SET IDENTITY_INSERT dbo.Genero ON;
INSERT INTO dbo.Genero (GeneroId, SteamId, Nombre, Slug) VALUES
    (1,  1, N'Acción',      N'accion'),
    (2, 25, N'Aventura',    N'aventura'),
    (3,  3, N'Rol',         N'rol'),
    (4,  2, N'Estrategia',  N'estrategia'),
    (5, 23, N'Indie',       N'indie'),
    (6, 28, N'Simuladores', N'simuladores'),
    (7,  4, N'Casual',      N'casual'),
    (8,  9, N'Carreras',    N'carreras'),
    (9, 18, N'Deportes',    N'deportes');
SET IDENTITY_INSERT dbo.Genero OFF;

SET IDENTITY_INSERT dbo.Plataforma ON;
INSERT INTO dbo.Plataforma (PlataformaId, Nombre, Slug) VALUES
    (1, N'Windows', N'windows'),
    (2, N'macOS',   N'mac'),
    (3, N'Linux',   N'linux');
SET IDENTITY_INSERT dbo.Plataforma OFF;

SET IDENTITY_INSERT dbo.Desarrolladora ON;
INSERT INTO dbo.Desarrolladora (DesarrolladoraId, Nombre, Slug) VALUES
    (1, N'CD PROJEKT RED',     N'cd-projekt-red'),
    (2, N'Rockstar North',     N'rockstar-north'),
    (3, N'Valve',              N'valve'),
    (4, N'FromSoftware, Inc.', N'fromsoftware-inc'),
    (5, N'Supergiant Games',   N'supergiant-games'),
    (6, N'Team Cherry',        N'team-cherry');
SET IDENTITY_INSERT dbo.Desarrolladora OFF;

SET IDENTITY_INSERT dbo.Juego ON;
INSERT INTO dbo.Juego (JuegoId, SteamAppId, Titulo, Slug, Sinopsis, FechaLanzamiento, PortadaUrl, Metacritic) VALUES
    (1, 292030, N'The Witcher 3: Wild Hunt', N'the-witcher-3-wild-hunt-292030',
        N'RPG de mundo abierto protagonizado por Geralt de Rivia.', '2015-05-18',
        N'https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/292030/4b5b1ba5061b8a992cc69ac58b4245434edb4296/header.jpg', 93),
    (2, 271590, N'Grand Theft Auto V', N'grand-theft-auto-v-271590',
        N'Acción en mundo abierto en la ciudad de Los Santos.', '2015-04-13',
        N'https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/271590/header.jpg', 96),
    (3, 620, N'Portal 2', N'portal-2-620',
        N'Puzzles en primera persona con el arma de portales.', '2011-04-18',
        N'https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/620/faffc0f560786e2f05104a8d2fac837c6969bf13/header.jpg', 95),
    (4, 1593500, N'God of War', N'god-of-war-1593500',
        NULL, '2022-01-14', NULL, 93),
    (5, 1245620, N'Elden Ring', N'elden-ring-1245620',
        N'Action RPG de FromSoftware en las Tierras Intermedias.', '2022-02-24',
        N'https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1245620/header.jpg', 94),
    (6, 1145360, N'Hades', N'hades-1145360',
        N'Roguelike: escapá del inframundo como Zagreo.', '2020-09-17',
        N'https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1145360/header.jpg', 93),
    (7, 367520, N'Hollow Knight', N'hollow-knight-367520',
        N'Metroidvania en el reino de Hallownest.', '2017-02-24',
        N'https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/367520/3c3489495136b26b34f8a9543c7f5645b99d388c/header.jpg', 87);
SET IDENTITY_INSERT dbo.Juego OFF;

INSERT INTO dbo.JuegoGenero (JuegoId, GeneroId) VALUES
    (1,3), (2,1),(2,2), (3,1),(3,2), (4,1),(4,2),(4,3),
    (5,1),(5,3), (6,1),(6,5),(6,3), (7,1),(7,2),(7,5);

INSERT INTO dbo.JuegoPlataforma (JuegoId, PlataformaId) VALUES
    (1,1), (2,1), (3,1),(3,3), (4,1), (5,1), (6,1),(6,2), (7,1),(7,2),(7,3);

INSERT INTO dbo.JuegoDesarrolladora (JuegoId, DesarrolladoraId) VALUES
    (1,1),(2,2),(3,3),(5,4),(6,5),(7,6);
GO

/* ------------------------------ Usuarios de prueba ------------------------------ */

SET IDENTITY_INSERT dbo.Usuario ON;
INSERT INTO dbo.Usuario (UsuarioId, Email, NombreUsuario, PasswordHash, RolId, Bloqueado,
                         AceptaPrivacidad, FechaConsentimiento, VersionPolitica) VALUES
    (1, N'superadmin@gamerank.test', N'superadmin', N'$2a$11$92Cdrfb4.lalSjIHtpgiYetiRPgWSRJbAsHka/F5cDXON8/g1XZ/q', 3, 0, 1, SYSUTCDATETIME(), '1.0'),
    (2, N'moderador@gamerank.test',  N'moderador',  N'$2a$11$92Cdrfb4.lalSjIHtpgiYetiRPgWSRJbAsHka/F5cDXON8/g1XZ/q', 2, 0, 1, SYSUTCDATETIME(), '1.0'),
    (3, N'lucia@gamerank.test',      N'lucia_rpg',  N'$2a$11$92Cdrfb4.lalSjIHtpgiYetiRPgWSRJbAsHka/F5cDXON8/g1XZ/q', 1, 0, 1, SYSUTCDATETIME(), '1.0'),
    (4, N'tomas@gamerank.test',      N'tomi_fps',   N'$2a$11$92Cdrfb4.lalSjIHtpgiYetiRPgWSRJbAsHka/F5cDXON8/g1XZ/q', 1, 0, 1, SYSUTCDATETIME(), '1.0'),
    (5, N'sofia@gamerank.test',      N'sofi_indie', N'$2a$11$92Cdrfb4.lalSjIHtpgiYetiRPgWSRJbAsHka/F5cDXON8/g1XZ/q', 1, 0, 1, SYSUTCDATETIME(), '1.0'),
    (6, N'spammer@gamerank.test',    N'spammer99',  N'$2a$11$92Cdrfb4.lalSjIHtpgiYetiRPgWSRJbAsHka/F5cDXON8/g1XZ/q', 1, 1, 1, SYSUTCDATETIME(), '1.0');
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
    ('PROGRAMADA', N'origen=destacados', SYSUTCDATETIME(), 'OK', 7, 0);
GO
