using BCrypt.Net;

namespace GameRank.Services;

public static class HashServicio
{
    private static readonly string HashFicticio = BCrypt.Net.BCrypt.HashPassword("sin-usuario");

    public static string Generar(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public static bool Verificar(string password, string? hash)
    {
        if (hash == null)
        {
            BCrypt.Net.BCrypt.Verify(password, HashFicticio);
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (SaltParseException)
        {
            return false;
        }
    }
}
