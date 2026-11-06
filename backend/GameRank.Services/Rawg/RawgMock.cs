using System.Reflection;
using System.Text.Json;

namespace GameRank.Services.Rawg;

public class RawgMock : IRawgCliente
{
    private readonly List<JuegoMock> _juegos;

    public RawgMock()
    {
        var ensamblado = Assembly.GetExecutingAssembly();
        var recurso = ensamblado.GetManifestResourceNames().Single(n => n.EndsWith("rawg-mock.json"));

        using var flujo = ensamblado.GetManifestResourceStream(recurso)!;
        using var documento = JsonDocument.Parse(flujo);

        _juegos = documento.RootElement.EnumerateArray()
            .Select(j => new JuegoMock(j.GetProperty("id").GetInt32(), j.GetProperty("name").GetString()!, j.GetRawText()))
            .ToList();
    }

    public Task<RawgLote> BuscarJuegos(string? texto, int pagina, int tamanio, int maxReintentos)
    {
        var coincidencias = _juegos
            .Where(j => string.IsNullOrEmpty(texto) || j.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var lote = new RawgLote
        {
            Juegos = coincidencias.Skip((pagina - 1) * tamanio).Take(tamanio).Select(j => j.Json).ToList(),
            HayMas = coincidencias.Count > pagina * tamanio
        };

        return Task.FromResult(lote);
    }

    public Task<string?> ObtenerJuego(int rawgId) =>
        Task.FromResult(_juegos.FirstOrDefault(j => j.RawgId == rawgId)?.Json);

    private record JuegoMock(int RawgId, string Nombre, string Json);
}
