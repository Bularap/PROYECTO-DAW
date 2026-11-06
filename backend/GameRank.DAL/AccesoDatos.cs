using GameRank.Services;
using Microsoft.Data.SqlClient;

namespace GameRank.DAL;

public class AccesoDatos
{
    private readonly string _cadena;

    public AccesoDatos(string cadena)
    {
        _cadena = cadena;
    }

    public async Task<T> Ejecutar<T>(Func<SqlConnection, Task<T>> operacion)
    {
        try
        {
            await using var conexion = new SqlConnection(_cadena);
            await conexion.OpenAsync();
            return await operacion(conexion);
        }
        catch (SqlException ex) when (ex.Number > 50000 && ex.Number < 50600)
        {
            throw new AppException(ex.Number - 50000, ex.Message);
        }
    }
}
