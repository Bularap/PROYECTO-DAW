using GameRank.BE;
using static GameRank.DAL.LectorDatos;

namespace GameRank.DAL;

public class ModeracionDAL
{
    private readonly AccesoDatos _datos;

    public ModeracionDAL(AccesoDatos datos)
    {
        _datos = datos;
    }

    public Task<ListaPaginada<ResenaModeracion>> ListarResenas(string filtro, int pagina, int tamanioPagina)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ListarModeracion");
            comando.Parameters.AddWithValue("@Filtro", filtro);
            comando.Parameters.AddWithValue("@Pagina", pagina);
            comando.Parameters.AddWithValue("@TamanioPagina", tamanioPagina);

            var resultado = new ListaPaginada<ResenaModeracion> { Pagina = pagina, TamanioPagina = tamanioPagina };

            await using var lector = await comando.ExecuteReaderAsync();
            while (await lector.ReadAsync())
            {
                resultado.Total = lector.GetInt32(lector.GetOrdinal("Total"));
                resultado.Items.Add(new ResenaModeracion
                {
                    ResenaId = lector.GetInt32(lector.GetOrdinal("ResenaId")),
                    JuegoId = lector.GetInt32(lector.GetOrdinal("JuegoId")),
                    Titulo = lector.GetString(lector.GetOrdinal("Titulo")),
                    UsuarioId = lector.GetInt32(lector.GetOrdinal("UsuarioId")),
                    NombreUsuario = lector.GetString(lector.GetOrdinal("NombreUsuario")),
                    AutorBloqueado = lector.GetBoolean(lector.GetOrdinal("AutorBloqueado")),
                    Contenido = lector.GetString(lector.GetOrdinal("Contenido")),
                    Estado = lector.GetString(lector.GetOrdinal("Estado")),
                    FechaPublicacion = FechaUtc(lector, "FechaPublicacion"),
                    CantidadReportes = lector.GetInt32(lector.GetOrdinal("CantidadReportes")),
                    UltimoReporte = FechaUtcONula(lector, "UltimoReporte"),
                    Motivos = TextoONulo(lector, "Motivos")
                });
            }

            return resultado;
        });
    }

    public Task Moderar(int adminId, int resenaId, string accion, string? motivo)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ModerarResena");
            comando.Parameters.AddWithValue("@AdminId", adminId);
            comando.Parameters.AddWithValue("@ResenaId", resenaId);
            comando.Parameters.AddWithValue("@Accion", accion);
            comando.Parameters.AddWithValue("@Motivo", (object?)motivo ?? DBNull.Value);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    public Task CambiarBloqueo(int adminId, int usuarioId, bool bloquear, string? motivo)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_CambiarBloqueoUsuario");
            comando.Parameters.AddWithValue("@AdminId", adminId);
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@Bloquear", bloquear);
            comando.Parameters.AddWithValue("@Motivo", (object?)motivo ?? DBNull.Value);
            return await comando.ExecuteNonQueryAsync();
        });
    }
}
