using System.Text.RegularExpressions;

namespace GameRank.Services;

public static partial class TextoServicio
{
    public static string Sanitizar(string? texto)
    {
        var limpio = ControlesInvalidos().Replace(texto ?? "", "");

        string anterior;
        do
        {
            anterior = limpio;
            limpio = EtiquetasHtml().Replace(limpio, "");
        }
        while (limpio != anterior);

        return limpio.Trim();
    }

    [GeneratedRegex(@"</?[a-zA-Z!?][^>]*>?")]
    private static partial Regex EtiquetasHtml();

    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]")]
    private static partial Regex ControlesInvalidos();
}
