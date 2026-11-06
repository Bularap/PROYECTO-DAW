using GameRank.DAL;
using GameRank.Services;
using GameRank.Services.Rawg;

namespace GameRank.BLL;

public class SincronizacionBLL
{
    public const string OrigenBusqueda = "BUSQUEDA";
    public const string OrigenProgramada = "PROGRAMADA";

    private readonly IRawgCliente _rawg;
    private readonly CatalogoDAL _catalogo;
    private readonly SincronizacionDAL _bitacora;

    public SincronizacionBLL(IRawgCliente rawg, CatalogoDAL catalogo, SincronizacionDAL bitacora)
    {
        _rawg = rawg;
        _catalogo = catalogo;
        _bitacora = bitacora;
    }

    public async Task<bool> Sincronizar(string origen, string? texto, int paginas, int tamanio, int maxReintentos)
    {
        var parametros = $"texto={texto}; paginas={paginas}; tamanio={tamanio}";
        var id = await _bitacora.Iniciar(origen, parametros.Length > 500 ? parametros[..500] : parametros);

        int insertados = 0, actualizados = 0, conError = 0, reintentos = 0;
        string? mensaje = null;
        var apiDisponible = true;

        for (var pagina = 1; pagina <= paginas; pagina++)
        {
            RawgLote lote;
            try
            {
                lote = await _rawg.BuscarJuegos(texto, pagina, tamanio, maxReintentos);
            }
            catch (RawgException ex)
            {
                reintentos += ex.Reintentos;
                mensaje = ex.Message;
                apiDisponible = false;
                break;
            }

            reintentos += lote.Reintentos;

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

            if (!lote.HayMas)
                break;
        }

        var estado = !apiDisponible
            ? (insertados + actualizados > 0 ? "PARCIAL" : "ERROR")
            : (conError > 0 ? "PARCIAL" : "OK");

        await _bitacora.Finalizar(id, estado, insertados, actualizados, conError, reintentos, mensaje);
        return apiDisponible;
    }
}
