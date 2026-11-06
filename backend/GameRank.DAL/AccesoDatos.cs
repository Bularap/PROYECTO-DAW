using System.Data;
using GameRank.Services;
using Microsoft.Data.SqlClient;

namespace GameRank.DAL;

public class AccesoDatos
{
    private const int ClaveDuplicada = 2627;
    private const int IndiceDuplicado = 2601;

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
        catch (SqlException ex) when (ex.Number == ClaveDuplicada || ex.Number == IndiceDuplicado)
        {
            throw new AppException(409, "El registro ya existe.");
        }
    }

    public static SqlCommand Procedimiento(SqlConnection conexion, string nombre) =>
        new(nombre, conexion) { CommandType = CommandType.StoredProcedure };
}
