using System.Reflection;
using System.Text.Json;

namespace GameRank.Services.Steam;

public class SteamMock : ISteamCliente
{
    private readonly List<JuegoMock> _juegos;

    public SteamMock()
    {
        var ensamblado = Assembly.GetExecutingAssembly();
        var recurso = ensamblado.GetManifestResourceNames().Single(n => n.EndsWith("steam-mock.json"));

        using var flujo = ensamblado.GetManifestResourceStream(recurso)!;
        using var documento = JsonDocument.Parse(flujo);

        _juegos = documento.RootElement.EnumerateArray()
            .Select(j => new JuegoMock(j.GetProperty("steam_appid").GetInt32(), j.GetProperty("name").GetString()!, j.GetRawText()))
            .ToList();
    }

    public Task<LoteSteam> Buscar(string texto, int maxReintentos) =>
        Task.FromResult(Lote(_juegos.Where(j => j.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase))));

    public Task<LoteSteam> ObtenerDestacados(int maxReintentos) => Task.FromResult(Lote(_juegos));

    public Task<string?> ObtenerJuego(int steamAppId)
    {
        var juego = _juegos.FirstOrDefault(j => j.SteamAppId == steamAppId);
        return Task.FromResult(juego == null ? null : SteamConversor.Normalizar(juego.Datos));
    }

    private static LoteSteam Lote(IEnumerable<JuegoMock> juegos) => new()
    {
        Juegos = juegos.Select(j => SteamConversor.Normalizar(j.Datos)).OfType<string>().ToList()
    };

    private record JuegoMock(int SteamAppId, string Nombre, string Datos);
}
