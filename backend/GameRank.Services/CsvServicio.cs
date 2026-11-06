using System.Globalization;

namespace GameRank.Services;

public static class CsvServicio
{
    private const string CaracteresDeFormula = "=+-@";

    public static string Fila(IEnumerable<object?> valores) => string.Join(",", valores.Select(Celda));

    private static string Celda(object? valor)
    {
        var texto = valor switch
        {
            null or DBNull => "",
            DateTime fecha => fecha.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            bool logico => logico ? "1" : "0",
            string cadena => NeutralizarFormula(cadena),
            IFormattable numero => numero.ToString(null, CultureInfo.InvariantCulture),
            _ => valor.ToString() ?? ""
        };

        return texto.IndexOfAny(['"', ',', '\n', '\r']) >= 0
            ? $"\"{texto.Replace("\"", "\"\"")}\""
            : texto;
    }

    private static string NeutralizarFormula(string texto) =>
        texto.Length > 0 && CaracteresDeFormula.Contains(texto[0]) ? "'" + texto : texto;
}
