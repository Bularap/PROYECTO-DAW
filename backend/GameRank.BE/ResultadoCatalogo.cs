namespace GameRank.BE;

public class ResultadoCatalogo
{
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanioPagina { get; set; }
    public bool Parcial { get; set; }
    public List<JuegoResumen> Juegos { get; set; } = [];
}
