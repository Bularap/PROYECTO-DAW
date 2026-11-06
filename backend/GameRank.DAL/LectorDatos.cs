using Microsoft.Data.SqlClient;

namespace GameRank.DAL;

public static class LectorDatos
{
    public static string? TextoONulo(SqlDataReader lector, string columna)
    {
        var indice = lector.GetOrdinal(columna);
        return lector.IsDBNull(indice) ? null : lector.GetString(indice);
    }

    public static decimal? DecimalONulo(SqlDataReader lector, string columna)
    {
        var indice = lector.GetOrdinal(columna);
        return lector.IsDBNull(indice) ? null : lector.GetDecimal(indice);
    }

    public static byte? ByteONulo(SqlDataReader lector, string columna)
    {
        var indice = lector.GetOrdinal(columna);
        return lector.IsDBNull(indice) ? null : lector.GetByte(indice);
    }

    public static int? EnteroONulo(SqlDataReader lector, string columna)
    {
        var indice = lector.GetOrdinal(columna);
        return lector.IsDBNull(indice) ? null : lector.GetInt32(indice);
    }

    public static DateTime FechaUtc(SqlDataReader lector, string columna) =>
        DateTime.SpecifyKind(lector.GetDateTime(lector.GetOrdinal(columna)), DateTimeKind.Utc);
}
