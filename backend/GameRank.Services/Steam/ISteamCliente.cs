namespace GameRank.Services.Steam;

public interface ISteamCliente
{
    Task<LoteSteam> Buscar(string texto, int maxReintentos);
    Task<LoteSteam> ObtenerDestacados(int maxReintentos);
    Task<string?> ObtenerJuego(int steamAppId);
}
