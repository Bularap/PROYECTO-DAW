using GameRank.BE;
using Microsoft.Data.SqlClient;

namespace GameRank.DAL;

public class SaludDAL
{
    private readonly AccesoDatos _datos;

    public SaludDAL(AccesoDatos datos)
    {
        _datos = datos;
    }

    public Task<EstadoBase> ObtenerEstado()
    {
        const string sql = @"SELECT DB_NAME(),
                                    (SELECT COUNT(*) FROM dbo.Juego),
                                    (SELECT COUNT(*) FROM dbo.Usuario)";

        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = new SqlCommand(sql, conexion);
            await using var lector = await comando.ExecuteReaderAsync();
            await lector.ReadAsync();
            return new EstadoBase
            {
                Base = lector.GetString(0),
                Juegos = lector.GetInt32(1),
                Usuarios = lector.GetInt32(2)
            };
        });
    }
}
