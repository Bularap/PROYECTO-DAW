using GameRank.DAL;
using GameRank.Services;
using GameRank.Services.Steam;

namespace GameRank.BLL;

public class SincronizacionBLL
{
    public const string OrigenBusqueda = "BUSQUEDA";
    public const string OrigenProgramada = "PROGRAMADA";

    private readonly ISteamCliente _steam;
    private readonly CatalogoDAL _catalogo;
    private readonly SincronizacionDAL _bitacora;

    public SincronizacionBLL(ISteamCliente steam, CatalogoDAL catalogo, SincronizacionDAL bitacora)
    {
        _steam = steam;
        _catalogo = catalogo;
        _bitacora = bitacora;
    }

    public async Task<bool> Sincronizar(string origen, string? texto, int maxReintentos)
    {
        var parametros = texto == null ? "origen=destacados" : $"texto={texto}";
        var id = await _bitacora.Iniciar(origen, parametros.Length > 500 ? parametros[..500] : parametros);

        LoteSteam lote;
        try
        {
            lote = texto == null
                ? await _steam.ObtenerDestacados(maxReintentos)
                : await _steam.Buscar(texto, maxReintentos);
        }
        catch (SteamException ex)
        {
            await _bitacora.Finalizar(id, "ERROR", 0, 0, 0, ex.Reintentos, ex.Message);
            return false;
        }

        int insertados = 0, actualizados = 0, conError = lote.Fallidos;
        string? mensaje = lote.Fallidos > 0 ? $"{lote.Fallidos} fichas de Steam no respondieron." : null;

        foreach (var json in lote.Juegos)
        {
            try
            {
                if (await _catalogo.GuardarDesdeApi(json))
                    insertados++;
                else
                    actualizados++;
            }
            catch (AppException ex)
            {
                conError++;
                mensaje = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            }
        }

        await _bitacora.Finalizar(id, conError > 0 ? "PARCIAL" : "OK", insertados, actualizados, conError, lote.Reintentos, mensaje);
        return true;
    }
}
