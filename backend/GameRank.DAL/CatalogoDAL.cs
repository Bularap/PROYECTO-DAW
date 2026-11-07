using System.Data;
using GameRank.BE;
using GameRank.Services;
using Microsoft.Data.SqlClient;
using static GameRank.DAL.LectorDatos;

namespace GameRank.DAL;

public class CatalogoDAL
{
    private readonly AccesoDatos _datos;

    public CatalogoDAL(AccesoDatos datos)
    {
        _datos = datos;
    }

    public Task<ResultadoCatalogo> Buscar(FiltroCatalogo filtro)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_BuscarCatalogo");
            comando.Parameters.AddWithValue("@Texto", (object?)filtro.Texto ?? DBNull.Value);
            comando.Parameters.AddWithValue("@Generos", (object?)filtro.Generos ?? DBNull.Value);
            comando.Parameters.AddWithValue("@Plataformas", (object?)filtro.Plataformas ?? DBNull.Value);
            comando.Parameters.AddWithValue("@PuntuacionMin", (object?)filtro.PuntuacionMin ?? DBNull.Value);
            comando.Parameters.AddWithValue("@PuntuacionMax", (object?)filtro.PuntuacionMax ?? DBNull.Value);
            comando.Parameters.AddWithValue("@Orden", filtro.Orden);
            comando.Parameters.AddWithValue("@Pagina", filtro.Pagina);
            comando.Parameters.AddWithValue("@TamanioPagina", filtro.TamanioPagina);

            var resultado = new ResultadoCatalogo { Pagina = filtro.Pagina, TamanioPagina = filtro.TamanioPagina };

            await using var lector = await comando.ExecuteReaderAsync();
            while (await lector.ReadAsync())
            {
                resultado.Total = lector.GetInt32(lector.GetOrdinal("TotalResultados"));
                var juego = new JuegoResumen();
                LeerResumen(lector, juego);
                resultado.Juegos.Add(juego);
            }

            return resultado;
        });
    }

    public Task<JuegoFicha> ObtenerFicha(int juegoId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ObtenerFichaJuego");
            comando.Parameters.AddWithValue("@JuegoId", juegoId);

            await using var lector = await comando.ExecuteReaderAsync();
            await lector.ReadAsync();

            var ficha = new JuegoFicha
            {
                Sinopsis = TextoONulo(lector, "Sinopsis"),
                Metacritic = ByteONulo(lector, "Metacritic"),
                FechaSincronizacion = FechaUtc(lector, "FechaSincronizacion")
            };
            LeerResumen(lector, ficha);

            await lector.NextResultAsync();
            ficha.Generos = await LeerItems(lector, "GeneroId");
            await lector.NextResultAsync();
            ficha.Plataformas = await LeerItems(lector, "PlataformaId");
            await lector.NextResultAsync();
            ficha.Desarrolladoras = await LeerItems(lector, "DesarrolladoraId");

            return ficha;
        });
    }

    public Task<List<ItemCatalogo>> ListarGeneros() => Listar("dbo.sp_ListarGeneros");

    public Task<List<ItemCatalogo>> ListarPlataformas() => Listar("dbo.sp_ListarPlataformas");

    public async Task<bool> GuardarDesdeApi(string json)
    {
        try
        {
            return await _datos.Ejecutar(async conexion =>
            {
                await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_UpsertJuegoDesdeApi");
                comando.Parameters.AddWithValue("@Json", json);
                var resultado = comando.Parameters.Add("@Resultado", SqlDbType.Char, 1);
                resultado.Direction = ParameterDirection.Output;

                await comando.ExecuteNonQueryAsync();
                return (string)resultado.Value == "I";
            });
        }
        catch (SqlException ex)
        {
            throw new AppException(500, ex.Message);
        }
    }

    private Task<List<ItemCatalogo>> Listar(string procedimiento)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, procedimiento);
            await using var lector = await comando.ExecuteReaderAsync();
            return await LeerItems(lector, "Id");
        });
    }

    private static async Task<List<ItemCatalogo>> LeerItems(SqlDataReader lector, string columnaId)
    {
        var items = new List<ItemCatalogo>();
        while (await lector.ReadAsync())
        {
            items.Add(new ItemCatalogo
            {
                Id = lector.GetInt32(lector.GetOrdinal(columnaId)),
                Nombre = lector.GetString(lector.GetOrdinal("Nombre")),
                Slug = lector.GetString(lector.GetOrdinal("Slug"))
            });
        }

        return items;
    }

    private static void LeerResumen(SqlDataReader lector, JuegoResumen juego)
    {
        var fecha = lector.GetOrdinal("FechaLanzamiento");

        juego.JuegoId = lector.GetInt32(lector.GetOrdinal("JuegoId"));
        juego.SteamAppId = lector.GetInt32(lector.GetOrdinal("SteamAppId"));
        juego.Titulo = lector.GetString(lector.GetOrdinal("Titulo"));
        juego.Slug = lector.GetString(lector.GetOrdinal("Slug"));
        juego.PortadaUrl = TextoONulo(lector, "PortadaUrl");
        juego.FechaLanzamiento = lector.IsDBNull(fecha) ? null : DateOnly.FromDateTime(lector.GetDateTime(fecha));
        juego.PromedioCalificacion = DecimalONulo(lector, "PromedioCalificacion");
        juego.CantidadCalificaciones = lector.GetInt32(lector.GetOrdinal("CantidadCalificaciones"));
    }
}
