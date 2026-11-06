using GameRank.BE;
using Microsoft.Data.SqlClient;
using static GameRank.DAL.LectorDatos;

namespace GameRank.DAL;

public class ValoracionDAL
{
    private readonly AccesoDatos _datos;

    public ValoracionDAL(AccesoDatos datos)
    {
        _datos = datos;
    }

    public Task<MetricasJuego?> Calificar(int usuarioId, int juegoId, byte puntuacion)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_CalificarJuego");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@JuegoId", juegoId);
            comando.Parameters.AddWithValue("@Puntuacion", puntuacion);
            return await LeerMetricas(comando);
        });
    }

    public Task<MetricasJuego?> EliminarCalificacion(int usuarioId, int juegoId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_EliminarCalificacion");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@JuegoId", juegoId);
            return await LeerMetricas(comando);
        });
    }

    public Task<Resena> CrearResena(int usuarioId, int juegoId, string contenido)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_CrearResena");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@JuegoId", juegoId);
            comando.Parameters.AddWithValue("@Contenido", contenido);
            return await LeerResena(comando);
        });
    }

    public Task<Resena> EditarResena(int usuarioId, int resenaId, string contenido)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_EditarResena");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@ResenaId", resenaId);
            comando.Parameters.AddWithValue("@Contenido", contenido);
            return await LeerResena(comando);
        });
    }

    public Task EliminarResena(int usuarioId, int resenaId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_EliminarResena");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@ResenaId", resenaId);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    public Task ReportarResena(int usuarioId, int resenaId, string motivo, string? comentario)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ReportarResena");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@ResenaId", resenaId);
            comando.Parameters.AddWithValue("@Motivo", motivo);
            comando.Parameters.AddWithValue("@Comentario", (object?)comentario ?? DBNull.Value);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    public Task<ResultadoFeed> ObtenerFeed(int juegoId, int pagina, int tamanioPagina)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ObtenerFeedJuego");
            comando.Parameters.AddWithValue("@JuegoId", juegoId);
            comando.Parameters.AddWithValue("@Pagina", pagina);
            comando.Parameters.AddWithValue("@TamanioPagina", tamanioPagina);

            var feed = new ResultadoFeed { Pagina = pagina, TamanioPagina = tamanioPagina };

            await using var lector = await comando.ExecuteReaderAsync();
            while (await lector.ReadAsync())
            {
                feed.Total = lector.GetInt32(lector.GetOrdinal("TotalResenas"));
                var resena = MapearResena(lector);
                resena.JuegoId = juegoId;
                feed.Resenas.Add(resena);
            }

            return feed;
        });
    }

    public Task<EstadoUsuarioJuego> ObtenerEstadoUsuario(int usuarioId, int juegoId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ObtenerEstadoUsuarioJuego");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@JuegoId", juegoId);

            await using var lector = await comando.ExecuteReaderAsync();
            await lector.ReadAsync();

            return new EstadoUsuarioJuego
            {
                Puntuacion = ByteONulo(lector, "Puntuacion"),
                EstadoListaId = ByteONulo(lector, "EstadoListaId"),
                ResenaId = EnteroONulo(lector, "ResenaId"),
                ResenaContenido = TextoONulo(lector, "ResenaContenido")
            };
        });
    }

    private static async Task<MetricasJuego?> LeerMetricas(SqlCommand comando)
    {
        await using var lector = await comando.ExecuteReaderAsync();
        if (!await lector.ReadAsync())
            return null;

        return new MetricasJuego
        {
            PromedioCalificacion = DecimalONulo(lector, "PromedioCalificacion"),
            CantidadCalificaciones = lector.GetInt32(lector.GetOrdinal("CantidadCalificaciones"))
        };
    }

    private static async Task<Resena> LeerResena(SqlCommand comando)
    {
        await using var lector = await comando.ExecuteReaderAsync();
        await lector.ReadAsync();

        var resena = MapearResena(lector);
        resena.JuegoId = lector.GetInt32(lector.GetOrdinal("JuegoId"));
        return resena;
    }

    private static Resena MapearResena(SqlDataReader lector) => new()
    {
        ResenaId = lector.GetInt32(lector.GetOrdinal("ResenaId")),
        UsuarioId = lector.GetInt32(lector.GetOrdinal("UsuarioId")),
        NombreUsuario = lector.GetString(lector.GetOrdinal("NombreUsuario")),
        AvatarUrl = TextoONulo(lector, "AvatarUrl"),
        Puntuacion = ByteONulo(lector, "Puntuacion"),
        Contenido = lector.GetString(lector.GetOrdinal("Contenido")),
        FechaPublicacion = FechaUtc(lector, "FechaPublicacion"),
        Editada = lector.GetBoolean(lector.GetOrdinal("Editada"))
    };
}
