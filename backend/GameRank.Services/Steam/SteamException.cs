namespace GameRank.Services.Steam;

public class SteamException : Exception
{
    public int Reintentos { get; }

    public SteamException(string mensaje, int reintentos = 0) : base(mensaje)
    {
        Reintentos = reintentos;
    }
}
