namespace GameRank.DAL;

public class SincronizacionDAL
{
    private readonly AccesoDatos _datos;

    public SincronizacionDAL(AccesoDatos datos)
    {
        _datos = datos;
    }

    public Task<int> Iniciar(string origen, string? parametros)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_IniciarSincronizacion");
            comando.Parameters.AddWithValue("@Origen", origen);
            comando.Parameters.AddWithValue("@ParametrosConsulta", (object?)parametros ?? DBNull.Value);
            return (int)(await comando.ExecuteScalarAsync())!;
        });
    }

    public Task Finalizar(int id, string estado, int insertados, int actualizados, int conError, int reintentos, string? mensaje)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_FinalizarSincronizacion");
            comando.Parameters.AddWithValue("@SincronizacionId", id);
            comando.Parameters.AddWithValue("@Estado", estado);
            comando.Parameters.AddWithValue("@Insertados", insertados);
            comando.Parameters.AddWithValue("@Actualizados", actualizados);
            comando.Parameters.AddWithValue("@ConError", conError);
            comando.Parameters.AddWithValue("@Reintentos", (byte)Math.Min(reintentos, 255));
            comando.Parameters.AddWithValue("@MensajeError", (object?)mensaje ?? DBNull.Value);
            return await comando.ExecuteNonQueryAsync();
        });
    }
}
