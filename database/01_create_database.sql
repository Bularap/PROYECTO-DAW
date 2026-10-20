/* =============================================================================
   GAME RANK - Plataforma Web Inteligente de Analitica de Habitos de Juego
   Script 01: Creacion de la base de datos
   Motor: Microsoft SQL Server 2019+
   ============================================================================= */

USE master;
GO

IF DB_ID(N'GameRank') IS NOT NULL
BEGIN
    ALTER DATABASE GameRank SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE GameRank;
END
GO

CREATE DATABASE GameRank COLLATE Modern_Spanish_CI_AS;
GO

-- Modelo de recuperacion FULL: necesario para backups de log (CU16 - Respaldo)
ALTER DATABASE GameRank SET RECOVERY FULL;
GO

USE GameRank;
GO
