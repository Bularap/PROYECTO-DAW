using System.Net;
using System.Text.Json;

namespace GameRank.Services.Steam;

public class SteamHttp : ISteamCliente
{
    private const int MaxResultadosBusqueda = 10;
    private static readonly string[] Categorias = ["top_sellers", "new_releases", "specials"];

    private readonly ConfiguracionSteam _config;
    private readonly HttpClient _http;

    public SteamHttp(ConfiguracionSteam config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(config.TimeoutSegundos) };
    }

    public async Task<LoteSteam> Buscar(string texto, int maxReintentos)
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSegundos));

        try
        {
            var (cuerpo, reintentos) = await Pedir($"{_config.TiendaUrl}/storesearch/?term={Uri.EscapeDataString(texto)}&l=spanish&cc=ar", maxReintentos, limite.Token);
            var ids = cuerpo == null ? [] : LeerIds(cuerpo, "items").Take(MaxResultadosBusqueda).ToList();

            var fichas = await Task.WhenAll(ids.Select(id => IntentarDetalle(id, 0, limite.Token)));
            return ArmarLote(fichas, reintentos);
        }
        catch (OperationCanceledException)
        {
            throw new SteamException("Steam no respondió a tiempo.");
        }
    }

    public async Task<LoteSteam> ObtenerDestacados(int maxReintentos)
    {
        var (cuerpo, reintentos) = await Pedir($"{_config.TiendaUrl}/featuredcategories?l=spanish&cc=ar", maxReintentos, CancellationToken.None);
        if (cuerpo == null)
            return new LoteSteam { Reintentos = reintentos };

        using var documento = JsonDocument.Parse(cuerpo);
        var ids = Categorias
            .Where(c => documento.RootElement.TryGetProperty(c, out _))
            .SelectMany(c => LeerIds(documento.RootElement.GetProperty(c).GetRawText(), "items"))
            .Distinct()
            .Take(_config.MaxJuegosPorCiclo)
            .ToList();

        var fichas = new List<(string? Juego, int Reintentos, bool Fallo)>();
        foreach (var id in ids)
            fichas.Add(await IntentarDetalle(id, maxReintentos, CancellationToken.None));

        return ArmarLote(fichas, reintentos);
    }

    public async Task<string?> ObtenerJuego(int steamAppId)
    {
        var (juego, _, fallo) = await IntentarDetalle(steamAppId, 0, CancellationToken.None);
        if (fallo)
            throw new SteamException("No se pudo obtener la ficha desde Steam.");

        return juego;
    }

    private static LoteSteam ArmarLote(IReadOnlyCollection<(string? Juego, int Reintentos, bool Fallo)> fichas, int reintentos)
    {
        var fallidos = fichas.Count(f => f.Fallo);
        if (fichas.Count > 0 && fallidos == fichas.Count)
            throw new SteamException("Steam no devolvió ninguna ficha.", reintentos + fichas.Sum(f => f.Reintentos));

        return new LoteSteam
        {
            Juegos = fichas.Select(f => f.Juego).OfType<string>().ToList(),
            Reintentos = reintentos + fichas.Sum(f => f.Reintentos),
            Fallidos = fallidos
        };
    }

    private async Task<(string? Juego, int Reintentos, bool Fallo)> IntentarDetalle(int appId, int maxReintentos, CancellationToken cancelacion)
    {
        try
        {
            var (cuerpo, reintentos) = await Pedir($"{_config.TiendaUrl}/appdetails?appids={appId}&l=spanish&cc=ar", maxReintentos, cancelacion);
            if (cuerpo == null)
                return (null, reintentos, false);

            using var documento = JsonDocument.Parse(cuerpo);
            if (!documento.RootElement.TryGetProperty(appId.ToString(), out var respuesta)
                || !respuesta.TryGetProperty("success", out var exito) || exito.ValueKind != JsonValueKind.True
                || !respuesta.TryGetProperty("data", out var datos))
                return (null, reintentos, false);

            return (SteamConversor.Normalizar(datos.GetRawText()), reintentos, false);
        }
        catch (SteamException ex)
        {
            return (null, ex.Reintentos, true);
        }
        catch (JsonException)
        {
            return (null, 0, true);
        }
    }

    private static IEnumerable<int> LeerIds(string cuerpo, string propiedad)
    {
        using var documento = JsonDocument.Parse(cuerpo);
        if (!documento.RootElement.TryGetProperty(propiedad, out var items) || items.ValueKind != JsonValueKind.Array)
            return [];

        return items.EnumerateArray()
            .Where(i => i.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
            .Select(i => i.GetProperty("id").GetInt32())
            .ToList();
    }

    private async Task<(string? Cuerpo, int Reintentos)> Pedir(string url, int maxReintentos, CancellationToken cancelacion)
    {
        var reintentos = 0;
        while (true)
        {
            HttpResponseMessage respuesta;
            try
            {
                respuesta = await _http.GetAsync(url, cancelacion);
            }
            catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
            {
                throw new SteamException("Steam no respondió a tiempo.", reintentos);
            }
            catch (HttpRequestException)
            {
                throw new SteamException("No se pudo conectar con Steam.", reintentos);
            }

            using (respuesta)
            {
                if (respuesta.StatusCode == HttpStatusCode.NotFound)
                    return (null, reintentos);

                if (respuesta.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (reintentos >= maxReintentos)
                        throw new SteamException("Steam rechazó el pedido por exceso de consultas.", reintentos);

                    reintentos++;
                    await Task.Delay(TimeSpan.FromSeconds(reintentos), cancelacion);
                    continue;
                }

                if (!respuesta.IsSuccessStatusCode)
                    throw new SteamException($"Steam respondió con el código {(int)respuesta.StatusCode}.", reintentos);

                return (await respuesta.Content.ReadAsStringAsync(cancelacion), reintentos);
            }
        }
    }
}
