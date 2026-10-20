/* =============================================================================
   GAME RANK - Ejecuta todos los scripts en orden.
   Requiere modo SQLCMD (SSMS: Query > SQLCMD Mode) o sqlcmd:
     sqlcmd -S (localdb)\MSSQLLocalDB -i 00_run_all.sql
   Ejecutar desde la carpeta database/. 06_carga_masiva.sql es opcional.
   ============================================================================= */

:on error exit

:r 01_create_database.sql
:r 02_tables.sql
:r 03_views.sql
:r 04_procedures.sql
:r 05_seed.sql
-- :r 06_carga_masiva.sql
