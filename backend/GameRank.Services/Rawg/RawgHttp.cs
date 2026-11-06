using System.Net;
using System.Text.Json;

namespace GameRank.Services.Rawg;

public class RawgHttp : IRawgCliente
{
    private readonly ConfiguracionRawg _config;
    private readonly HttpClient _http;

    public RawgHttp(ConfiguracionRawg config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(config.TimeoutSegundos) };
    }

    public async Task<RawgLote> BuscarJuegos(string? texto, int pagina, int tamanio, int maxReintentos)
    {
        var url = $"{_config.BaseUrl}/games?key={Uri.EscapeDataString(_config.ApiKey)}&page={pagina}&page_size={tamanio}";
        if (!string.IsNullOrEmpty(texto))
            url += $"&search={Uri.EscapeDataString(texto)}";

        var (cuerpo, reintentos) = await Pedir(url, maxReintentos);
        if (cuerpo == null)
            return new RawgLote { Reintentos = reintentos };

        using var documento = JsonDocument.Parse(cuerpo);
        var raiz = documento.RootElement;

        return new RawgLote
        {
            Juegos = raiz.GetProperty("results").EnumerateArray().Select(j => j.GetRawText()).ToList(),
            HayMas = raiz.TryGetProperty("next", out var siguiente) && siguiente.ValueKind == JsonValueKind.String,
            Reintentos = reintentos
        };
    }

    public async Task<string?> ObtenerJuego(int rawgId)
    {
        var url = $"{_config.BaseUrl}/games/{rawgId}?key={Uri.EscapeDataString(_config.ApiKey)}";
        var (cuerpo, _) = await Pedir(url, 0);
        return cuerpo;
    }

    private async Task<(string? Cuerpo, int Reintentos)> Pedir(string url, int maxReintentos)
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
            throw new RawgException("No hay una clave de RAWG configurada.");

        var reintentos = 0;
        while (true)
        {
            HttpResponseMessage respuesta;
            try
            {
                respuesta = await _http.GetAsync(url);
            }
            catch (TaskCanceledException)
            {
                throw new RawgException("RAWG no respondió a tiempo.", reintentos);
            }
            catch (HttpRequestException)
            {
                throw new RawgException("No se pudo conectar con RAWG.", reintentos);
            }

            using (respuesta)
            {
                if (respuesta.StatusCode == HttpStatusCode.NotFound)
                    return (null, reintentos);

                if (respuesta.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (reintentos >= maxReintentos)
                        throw new RawgException("RAWG rechazó el pedido por exceso de consultas.", reintentos);

                    reintentos++;
                    await Task.Delay(TimeSpan.FromSeconds(reintentos));
                    continue;
                }

                if (!respuesta.IsSuccessStatusCode)
                    throw new RawgException($"RAWG respondió con el código {(int)respuesta.StatusCode}.", reintentos);

                return (await respuesta.Content.ReadAsStringAsync(), reintentos);
            }
        }
    }
}
