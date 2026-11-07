using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameRank.Services.Steam;

public static partial class SteamConversor
{
    private static readonly Dictionary<string, int> Meses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ENE"] = 1, ["JAN"] = 1, ["FEB"] = 2, ["MAR"] = 3, ["ABR"] = 4, ["APR"] = 4, ["MAY"] = 5,
        ["JUN"] = 6, ["JUL"] = 7, ["AGO"] = 8, ["AUG"] = 8, ["SEP"] = 9, ["SEPT"] = 9, ["SET"] = 9,
        ["OCT"] = 10, ["NOV"] = 11, ["DIC"] = 12, ["DEC"] = 12
    };

    private static readonly (string Clave, string Nombre)[] Plataformas =
        [("windows", "Windows"), ("mac", "macOS"), ("linux", "Linux")];

    public static string? Normalizar(string datosSteam)
    {
        using var documento = JsonDocument.Parse(datosSteam);
        var datos = documento.RootElement;

        if (Texto(datos, "type") is { } tipo && tipo != "game")
            return null;

        var appId = datos.GetProperty("steam_appid").GetInt32();
        var nombre = Texto(datos, "name")?.Trim();

        var juego = new Dictionary<string, object?>
        {
            ["steamAppId"] = appId,
            ["nombre"] = string.IsNullOrEmpty(nombre) ? null : Recortar(nombre, 255),
            ["slug"] = string.IsNullOrEmpty(nombre) ? null : Recortar(Slug(nombre), 240) + "-" + appId,
            ["sinopsis"] = Sinopsis(Texto(datos, "short_description")),
            ["lanzamiento"] = Fecha(datos),
            ["portada"] = Texto(datos, "header_image"),
            ["metacritic"] = datos.TryGetProperty("metacritic", out var mc) && mc.TryGetProperty("score", out var puntaje) && puntaje.ValueKind == JsonValueKind.Number
                ? puntaje.GetInt32()
                : null
        };

        if (datos.TryGetProperty("genres", out var generos) && generos.ValueKind == JsonValueKind.Array)
        {
            juego["generos"] = generos.EnumerateArray()
                .Where(g => int.TryParse(Texto(g, "id"), out _) && !string.IsNullOrWhiteSpace(Texto(g, "description")))
                .Select(g => new
                {
                    id = int.Parse(Texto(g, "id")!),
                    nombre = Recortar(Texto(g, "description")!.Trim(), 60),
                    slug = Recortar(Slug(Texto(g, "description")!), 60)
                })
                .ToList();
        }

        if (datos.TryGetProperty("platforms", out var plataformas) && plataformas.ValueKind == JsonValueKind.Object)
        {
            juego["plataformas"] = Plataformas
                .Where(p => plataformas.TryGetProperty(p.Clave, out var valor) && valor.ValueKind == JsonValueKind.True)
                .Select(p => new { nombre = p.Nombre, slug = p.Clave })
                .ToList();
        }

        if (datos.TryGetProperty("developers", out var desarrolladoras) && desarrolladoras.ValueKind == JsonValueKind.Array)
        {
            juego["desarrolladoras"] = desarrolladoras.EnumerateArray()
                .Where(d => d.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(d.GetString()) && Slug(d.GetString()!).Length > 0)
                .Select(d => new { nombre = Recortar(d.GetString()!.Trim(), 150), slug = Recortar(Slug(d.GetString()!), 150) })
                .ToList();
        }

        return JsonSerializer.Serialize(juego);
    }

    public static string Slug(string texto)
    {
        var sinAcentos = new StringBuilder();
        foreach (var caracter in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                sinAcentos.Append(caracter);
        }

        return NoAlfanumerico().Replace(sinAcentos.ToString().ToLowerInvariant(), "-").Trim('-');
    }

    private static string? Fecha(JsonElement datos)
    {
        if (!datos.TryGetProperty("release_date", out var lanzamiento))
            return null;
        if (lanzamiento.TryGetProperty("coming_soon", out var proximamente) && proximamente.ValueKind == JsonValueKind.True)
            return null;

        var coincidencia = FormatoFecha().Match(Texto(lanzamiento, "date") ?? "");
        if (!coincidencia.Success || !Meses.TryGetValue(coincidencia.Groups[2].Value.TrimEnd('.'), out var mes))
            return null;

        var dia = int.Parse(coincidencia.Groups[1].Value);
        var anio = int.Parse(coincidencia.Groups[3].Value);
        if (dia > DateTime.DaysInMonth(anio, mes))
            return null;

        return new DateOnly(anio, mes, dia).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string? Sinopsis(string? texto)
    {
        var limpio = TextoServicio.Sanitizar(WebUtility.HtmlDecode(texto ?? ""));
        return limpio.Length == 0 ? null : limpio;
    }

    private static string? Texto(JsonElement elemento, string propiedad) =>
        elemento.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    private static string Recortar(string texto, int largo) => texto.Length > largo ? texto[..largo].TrimEnd('-') : texto;

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NoAlfanumerico();

    [GeneratedRegex(@"^\s*(\d{1,2})\s+([A-Za-z]+\.?),?\s+(\d{4})\s*$")]
    private static partial Regex FormatoFecha();
}
