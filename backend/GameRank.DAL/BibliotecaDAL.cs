using GameRank.BE;
using static GameRank.DAL.LectorDatos;

namespace GameRank.DAL;

public class BibliotecaDAL
{
    private readonly AccesoDatos _datos;

    public BibliotecaDAL(AccesoDatos datos)
    {
        _datos = datos;
    }

    public Task<List<EstadoLista>> ListarEstados()
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ListarEstadosLista");
            await using var lector = await comando.ExecuteReaderAsync();

            var estados = new List<EstadoLista>();
            while (await lector.ReadAsync())
            {
                estados.Add(new EstadoLista
                {
                    EstadoListaId = lector.GetByte(lector.GetOrdinal("EstadoListaId")),
                    Nombre = lector.GetString(lector.GetOrdinal("Nombre"))
                });
            }

            return estados;
        });
    }

    public Task Asignar(int usuarioId, int juegoId, byte estadoListaId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_AsignarEstadoBiblioteca");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@JuegoId", juegoId);
            comando.Parameters.AddWithValue("@EstadoListaId", estadoListaId);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    public Task Quitar(int usuarioId, int juegoId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_QuitarDeBiblioteca");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@JuegoId", juegoId);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    public Task<List<ItemBiblioteca>> Listar(int usuarioId, byte? estadoListaId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ListarBiblioteca");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@EstadoListaId", (object?)estadoListaId ?? DBNull.Value);

            await using var lector = await comando.ExecuteReaderAsync();

            var items = new List<ItemBiblioteca>();
            while (await lector.ReadAsync())
            {
                items.Add(new ItemBiblioteca
                {
                    JuegoId = lector.GetInt32(lector.GetOrdinal("JuegoId")),
                    Titulo = lector.GetString(lector.GetOrdinal("Titulo")),
                    PortadaUrl = TextoONulo(lector, "PortadaUrl"),
                    EstadoListaId = lector.GetByte(lector.GetOrdinal("EstadoListaId")),
                    Estado = lector.GetString(lector.GetOrdinal("Estado")),
                    Fecha = FechaUtc(lector, "Fecha")
                });
            }

            return items;
        });
    }
}
