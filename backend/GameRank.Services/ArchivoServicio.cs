using System.Security.Cryptography;

namespace GameRank.Services;

public static class ArchivoServicio
{
    public static bool PuedeLeerse(string ruta)
    {
        try
        {
            using var flujo = File.OpenRead(ruta);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static long? Tamanio(string ruta)
    {
        try
        {
            return new FileInfo(ruta).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string? Sha256(string ruta)
    {
        try
        {
            using var flujo = File.OpenRead(ruta);
            return Convert.ToHexString(SHA256.HashData(flujo)).ToLowerInvariant();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void EliminarSiExiste(string ruta)
    {
        try
        {
            File.Delete(ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
