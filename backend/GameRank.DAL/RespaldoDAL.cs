using GameRank.BE;
using GameRank.Services;
using Microsoft.Data.SqlClient;
using static GameRank.DAL.LectorDatos;

namespace GameRank.DAL;

public class RespaldoDAL
{
    private const int SegundosLimite = 1800;

    private static readonly Dictionary<string, string> TablasExportables = new()
    {
        ["Juego"] = "SELECT JuegoId, RawgId, Titulo, Slug, Sinopsis, FechaLanzamiento, PortadaUrl, RatingRawg, Metacritic FROM dbo.Juego",
        ["Genero"] = "SELECT GeneroId, RawgId, Nombre, Slug FROM dbo.Genero",
        ["Plataforma"] = "SELECT PlataformaId, RawgId, Nombre, Slug FROM dbo.Plataforma",
        ["Desarrolladora"] = "SELECT DesarrolladoraId, RawgId, Nombre, Slug FROM dbo.Desarrolladora",
        ["JuegoGenero"] = "SELECT JuegoId, GeneroId FROM dbo.JuegoGenero",
        ["JuegoPlataforma"] = "SELECT JuegoId, PlataformaId FROM dbo.JuegoPlataforma",
        ["JuegoDesarrolladora"] = "SELECT JuegoId, DesarrolladoraId FROM dbo.JuegoDesarrolladora",
        ["Usuario"] = "SELECT UsuarioId, NombreUsuario, RolId, Bloqueado, FechaRegistro FROM dbo.Usuario",
        ["Calificacion"] = "SELECT UsuarioId, JuegoId, Puntuacion, FechaCalificacion, FechaModificacion FROM dbo.Calificacion",
        ["Resena"] = "SELECT ResenaId, UsuarioId, JuegoId, Contenido, Estado, FechaPublicacion, FechaModificacion FROM dbo.Resena",
        ["BibliotecaUsuario"] = "SELECT UsuarioId, JuegoId, EstadoListaId, FechaAgregado, FechaModificacion FROM dbo.BibliotecaUsuario"
    };

    private readonly AccesoDatos _datos;

    public RespaldoDAL(AccesoDatos datos)
    {
        _datos = datos;
    }

    public IEnumerable<string> NombresTablasExportables => TablasExportables.Keys;

    public Task<int> Iniciar(int usuarioId, string tipo, string formato, string ruta)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_IniciarRespaldo");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@Tipo", tipo);
            comando.Parameters.AddWithValue("@Formato", formato);
            comando.Parameters.AddWithValue("@RutaArchivo", ruta);
            return (int)(await comando.ExecuteScalarAsync())!;
        });
    }

    public Task Finalizar(int id, string estado, long? tamanio, string? checksum, string? mensaje)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_FinalizarRespaldo");
            comando.Parameters.AddWithValue("@RespaldoId", id);
            comando.Parameters.AddWithValue("@Estado", estado);
            comando.Parameters.AddWithValue("@TamanioBytes", (object?)tamanio ?? DBNull.Value);
            comando.Parameters.AddWithValue("@ChecksumSha256", (object?)checksum ?? DBNull.Value);
            comando.Parameters.AddWithValue("@MensajeError", (object?)mensaje ?? DBNull.Value);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    public async Task EjecutarBackup(string tipo, string ruta)
    {
        try
        {
            await _datos.Ejecutar(async conexion =>
            {
                await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_EjecutarBackup");
                comando.CommandTimeout = SegundosLimite;
                comando.Parameters.AddWithValue("@Tipo", tipo);
                comando.Parameters.AddWithValue("@Ruta", ruta);
                return await comando.ExecuteNonQueryAsync();
            });
        }
        catch (SqlException ex)
        {
            throw new AppException(500, ex.Message);
        }
    }

    public Task<ListaPaginada<Respaldo>> Listar(int pagina, int tamanioPagina)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ListarRespaldos");
            comando.Parameters.AddWithValue("@Pagina", pagina);
            comando.Parameters.AddWithValue("@TamanioPagina", tamanioPagina);

            var resultado = new ListaPaginada<Respaldo> { Pagina = pagina, TamanioPagina = tamanioPagina };

            await using var lector = await comando.ExecuteReaderAsync();
            while (await lector.ReadAsync())
            {
                resultado.Total = lector.GetInt32(lector.GetOrdinal("Total"));
                resultado.Items.Add(new Respaldo
                {
                    RespaldoId = lector.GetInt32(lector.GetOrdinal("RespaldoId")),
                    UsuarioId = lector.GetInt32(lector.GetOrdinal("UsuarioId")),
                    NombreUsuario = lector.GetString(lector.GetOrdinal("NombreUsuario")),
                    Tipo = lector.GetString(lector.GetOrdinal("Tipo")),
                    Formato = lector.GetString(lector.GetOrdinal("Formato")),
                    RutaArchivo = lector.GetString(lector.GetOrdinal("RutaArchivo")),
                    TamanioBytes = lector.IsDBNull(lector.GetOrdinal("TamanioBytes")) ? null : lector.GetInt64(lector.GetOrdinal("TamanioBytes")),
                    ChecksumSha256 = TextoONulo(lector, "ChecksumSha256"),
                    Estado = lector.GetString(lector.GetOrdinal("Estado")),
                    FechaInicio = FechaUtc(lector, "FechaInicio"),
                    FechaFin = FechaUtcONula(lector, "FechaFin"),
                    MensajeError = TextoONulo(lector, "MensajeError")
                });
            }

            return resultado;
        });
    }

    public Task<string?> ObtenerDirectorioPredeterminado()
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = new SqlCommand("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(400))", conexion);
            var valor = await comando.ExecuteScalarAsync();
            return valor is DBNull ? null : (string?)valor;
        });
    }

    public async Task CrearDirectorio(string directorio)
    {
        try
        {
            await _datos.EjecutarEnMaster(async (conexion, _) =>
            {
                await using var comando = new SqlCommand("EXEC master.dbo.xp_create_subdir @Directorio", conexion);
                comando.Parameters.AddWithValue("@Directorio", directorio);
                return await comando.ExecuteNonQueryAsync();
            });
        }
        catch (SqlException ex)
        {
            throw new AppException(500, ex.Message);
        }
    }

    public async Task Verificar(string ruta)
    {
        try
        {
            await _datos.EjecutarEnMaster(async (conexion, _) =>
            {
                await using var comando = new SqlCommand("RESTORE VERIFYONLY FROM DISK = @Ruta WITH CHECKSUM", conexion);
                comando.CommandTimeout = SegundosLimite;
                comando.Parameters.AddWithValue("@Ruta", ruta);
                return await comando.ExecuteNonQueryAsync();
            });
        }
        catch (SqlException)
        {
            throw new AppException(409, "El archivo de respaldo está dañado. No se realizó la restauración.");
        }
    }

    public async Task Restaurar(IReadOnlyList<Respaldo> cadena)
    {
        try
        {
            await _datos.EjecutarEnMaster(async (conexion, baseDatos) =>
            {
                var nombre = "[" + baseDatos.Replace("]", "]]") + "]";
                AccesoDatos.LimpiarConexiones();

                try
                {
                    for (var i = 0; i < cadena.Count; i++)
                    {
                        var ultimo = i == cadena.Count - 1;
                        var recuperacion = ultimo ? "RECOVERY" : "NORECOVERY";
                        var sentencia = cadena[i].Tipo == "LOG"
                            ? $"RESTORE LOG {nombre} FROM DISK = @Ruta WITH {recuperacion}, CHECKSUM"
                            : $"RESTORE DATABASE {nombre} FROM DISK = @Ruta WITH REPLACE, {recuperacion}, CHECKSUM";

                        if (i == 0)
                            sentencia = $"IF DATABASEPROPERTYEX(@Base, 'Status') = 'ONLINE' ALTER DATABASE {nombre} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; {sentencia}";

                        await using var comando = new SqlCommand(sentencia, conexion);
                        comando.CommandTimeout = SegundosLimite;
                        comando.Parameters.AddWithValue("@Ruta", cadena[i].RutaArchivo);
                        comando.Parameters.AddWithValue("@Base", baseDatos);
                        await comando.ExecuteNonQueryAsync();
                    }
                }
                finally
                {
                    await PermitirConexiones(conexion, nombre);
                    AccesoDatos.LimpiarConexiones();
                }

                return 0;
            });
        }
        catch (SqlException ex)
        {
            throw new AppException(500, ex.Message);
        }
    }

    public Task Reponer(Respaldo respaldo)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ReponerRespaldo");
            comando.Parameters.AddWithValue("@RespaldoId", respaldo.RespaldoId);
            comando.Parameters.AddWithValue("@UsuarioId", respaldo.UsuarioId);
            comando.Parameters.AddWithValue("@Tipo", respaldo.Tipo);
            comando.Parameters.AddWithValue("@Formato", respaldo.Formato);
            comando.Parameters.AddWithValue("@RutaArchivo", respaldo.RutaArchivo);
            comando.Parameters.AddWithValue("@TamanioBytes", (object?)respaldo.TamanioBytes ?? DBNull.Value);
            comando.Parameters.AddWithValue("@ChecksumSha256", (object?)respaldo.ChecksumSha256 ?? DBNull.Value);
            comando.Parameters.AddWithValue("@Estado", respaldo.Estado);
            comando.Parameters.AddWithValue("@FechaInicio", respaldo.FechaInicio);
            comando.Parameters.AddWithValue("@FechaFin", (object?)respaldo.FechaFin ?? DBNull.Value);
            comando.Parameters.AddWithValue("@MensajeError", (object?)respaldo.MensajeError ?? DBNull.Value);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    public Task RegistrarRestauracion(int usuarioId, int respaldoId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_RegistrarRestauracion");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@RespaldoId", respaldoId);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    public Task EscribirTablaCsv(string tabla, TextWriter destino)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = new SqlCommand(TablasExportables[tabla], conexion);
            comando.CommandTimeout = SegundosLimite;

            await using var lector = await comando.ExecuteReaderAsync();

            var columnas = Enumerable.Range(0, lector.FieldCount).Select(lector.GetName);
            await destino.WriteLineAsync(CsvServicio.Fila(columnas));

            var valores = new object[lector.FieldCount];
            while (await lector.ReadAsync())
            {
                lector.GetValues(valores);
                await destino.WriteLineAsync(CsvServicio.Fila(valores));
            }

            return 0;
        });
    }

    private static async Task PermitirConexiones(SqlConnection conexion, string nombre)
    {
        try
        {
            await using var comando = new SqlCommand($"ALTER DATABASE {nombre} SET MULTI_USER", conexion);
            await comando.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
        }
    }
}
