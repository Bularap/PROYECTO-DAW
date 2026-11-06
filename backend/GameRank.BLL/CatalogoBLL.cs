using GameRank.BE;
using GameRank.DAL;
using GameRank.Services;
using GameRank.Services.Rawg;

namespace GameRank.BLL;

public class CatalogoBLL
{
    private const int DiasVigencia = 30;
    private const int TamanioLoteBusqueda = 20;
    private static readonly string[] OrdenesValidos = ["TITULO", "PUNTUACION", "LANZAMIENTO"];

    private readonly CatalogoDAL _dal;
    private readonly SincronizacionBLL _sincronizacion;
    private readonly IRawgCliente _rawg;

    public CatalogoBLL(CatalogoDAL dal, SincronizacionBLL sincronizacion, IRawgCliente rawg)
    {
        _dal = dal;
        _sincronizacion = sincronizacion;
        _rawg = rawg;
    }

    public async Task<ResultadoCatalogo> Buscar(FiltroCatalogo filtro)
    {
        Normalizar(filtro);

        var resultado = await _dal.Buscar(filtro);
        if (resultado.Total > 0 || !EsBusquedaSoloPorTexto(filtro))
            return resultado;

        var apiDisponible = await _sincronizacion.Sincronizar(SincronizacionBLL.OrigenBusqueda, filtro.Texto, 1, TamanioLoteBusqueda, 0);

        resultado = await _dal.Buscar(filtro);
        resultado.Parcial = !apiDisponible;
        return resultado;
    }

    public async Task<JuegoFicha> ObtenerFicha(int juegoId)
    {
        var ficha = await _dal.ObtenerFicha(juegoId);
        var vencida = ficha.FechaSincronizacion < DateTime.UtcNow.AddDays(-DiasVigencia);

        if (ficha.Sinopsis != null && !vencida)
            return ficha;

        try
        {
            var json = await _rawg.ObtenerJuego(ficha.RawgId);
            if (json == null)
                return ficha;

            await _dal.GuardarDesdeApi(json);
            return await _dal.ObtenerFicha(juegoId);
        }
        catch (Exception ex) when (ex is RawgException or AppException)
        {
            return ficha;
        }
    }

    public Task<List<ItemCatalogo>> ListarGeneros() => _dal.ListarGeneros();

    public Task<List<ItemCatalogo>> ListarPlataformas() => _dal.ListarPlataformas();

    private static void Normalizar(FiltroCatalogo filtro)
    {
        filtro.Texto = NuloSiVacio(filtro.Texto);
        filtro.Generos = NuloSiVacio(filtro.Generos);
        filtro.Plataformas = NuloSiVacio(filtro.Plataformas);
        filtro.Orden = NuloSiVacio(filtro.Orden)?.ToUpperInvariant() ?? "TITULO";

        if (filtro.Texto?.Length > 100)
            throw new AppException(400, "La búsqueda no puede superar los 100 caracteres.");
        if (filtro.Generos?.Length > 500 || filtro.Plataformas?.Length > 500)
            throw new AppException(400, "Se seleccionaron demasiados filtros.");
        if (!OrdenesValidos.Contains(filtro.Orden))
            throw new AppException(400, "El orden indicado no es válido.");
        if (filtro.PuntuacionMin is < 1 or > 5 || filtro.PuntuacionMax is < 1 or > 5)
            throw new AppException(400, "La puntuación debe estar entre 1 y 5.");
        if (filtro.PuntuacionMin > filtro.PuntuacionMax)
            throw new AppException(400, "La puntuación mínima no puede superar a la máxima.");
        if (filtro.Pagina < 1)
            throw new AppException(400, "La página debe ser mayor o igual a 1.");
        if (filtro.TamanioPagina < 1 || filtro.TamanioPagina > 100)
            throw new AppException(400, "El tamaño de página debe estar entre 1 y 100.");
    }

    private static bool EsBusquedaSoloPorTexto(FiltroCatalogo filtro) =>
        filtro.Texto != null
        && filtro.Pagina == 1
        && filtro.Generos == null
        && filtro.Plataformas == null
        && filtro.PuntuacionMin == null
        && filtro.PuntuacionMax == null;

    private static string? NuloSiVacio(string? valor)
    {
        var limpio = valor?.Trim();
        return string.IsNullOrEmpty(limpio) ? null : limpio;
    }
}
