namespace GameRank.Services.Steam;

public class LoteSteam
{
    public List<string> Juegos { get; set; } = [];
    public int Reintentos { get; set; }
    public int Fallidos { get; set; }
}
