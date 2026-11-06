namespace GameRank.Services;

public class AppException : Exception
{
    public int CodigoHttp { get; }

    public AppException(int codigoHttp, string mensaje) : base(mensaje)
    {
        CodigoHttp = codigoHttp;
    }
}
