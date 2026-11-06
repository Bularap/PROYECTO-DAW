namespace GameRank.Services.Rawg;

public class RawgLote
{
    public List<string> Juegos { get; set; } = [];
    public bool HayMas { get; set; }
    public int Reintentos { get; set; }
}
