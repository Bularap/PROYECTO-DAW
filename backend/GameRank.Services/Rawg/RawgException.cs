namespace GameRank.Services.Rawg;

public class RawgException : Exception
{
    public int Reintentos { get; }

    public RawgException(string mensaje, int reintentos = 0) : base(mensaje)
    {
        Reintentos = reintentos;
    }
}
