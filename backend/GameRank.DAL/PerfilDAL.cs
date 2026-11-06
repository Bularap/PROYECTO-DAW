using GameRank.BE;
using static GameRank.DAL.LectorDatos;

namespace GameRank.DAL;

public class PerfilDAL
{
    private readonly AccesoDatos _datos;

    public PerfilDAL(AccesoDatos datos)
    {
        _datos = datos;
    }

    public Task<Perfil> Obtener(int usuarioId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ObtenerPerfil");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);

            await using var lector = await comando.ExecuteReaderAsync();
            await lector.ReadAsync();

            var perfil = new Perfil
            {
                UsuarioId = lector.GetInt32(lector.GetOrdinal("UsuarioId")),
                NombreUsuario = lector.GetString(lector.GetOrdinal("NombreUsuario")),
                AvatarUrl = TextoONulo(lector, "AvatarUrl"),
                FechaRegistro = FechaUtc(lector, "FechaRegistro"),
                PromedioOtorgado = DecimalONulo(lector, "PromedioOtorgado"),
                CantidadCalificaciones = lector.GetInt32(lector.GetOrdinal("CantidadCalificaciones")),
                CantidadResenas = lector.GetInt32(lector.GetOrdinal("CantidadResenas"))
            };

            await lector.NextResultAsync();
            while (await lector.ReadAsync())
            {
                perfil.Listas.Add(new CantidadLista
                {
                    EstadoListaId = lector.GetByte(lector.GetOrdinal("EstadoListaId")),
                    Nombre = lector.GetString(lector.GetOrdinal("Nombre")),
                    Cantidad = lector.GetInt32(lector.GetOrdinal("Cantidad"))
                });
            }

            await lector.NextResultAsync();
            while (await lector.ReadAsync())
            {
                perfil.Calificaciones.Add(new CalificacionPerfil
                {
                    JuegoId = lector.GetInt32(lector.GetOrdinal("JuegoId")),
                    Titulo = lector.GetString(lector.GetOrdinal("Titulo")),
                    PortadaUrl = TextoONulo(lector, "PortadaUrl"),
                    Puntuacion = lector.GetByte(lector.GetOrdinal("Puntuacion")),
                    Fecha = FechaUtc(lector, "Fecha")
                });
            }

            await lector.NextResultAsync();
            while (await lector.ReadAsync())
            {
                perfil.Resenas.Add(new ResenaPerfil
                {
                    ResenaId = lector.GetInt32(lector.GetOrdinal("ResenaId")),
                    JuegoId = lector.GetInt32(lector.GetOrdinal("JuegoId")),
                    Titulo = lector.GetString(lector.GetOrdinal("Titulo")),
                    Puntuacion = ByteONulo(lector, "Puntuacion"),
                    Contenido = lector.GetString(lector.GetOrdinal("Contenido")),
                    FechaPublicacion = FechaUtc(lector, "FechaPublicacion"),
                    Editada = lector.GetBoolean(lector.GetOrdinal("Editada"))
                });
            }

            return perfil;
        });
    }
}
