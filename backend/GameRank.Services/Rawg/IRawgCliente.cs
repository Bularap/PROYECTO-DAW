namespace GameRank.Services.Rawg;

public interface IRawgCliente
{
    Task<RawgLote> BuscarJuegos(string? texto, int pagina, int tamanio, int maxReintentos);
    Task<string?> ObtenerJuego(int rawgId);
}
