namespace GameRank.BE;

public class ResultadoFeed
{
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanioPagina { get; set; }
    public List<Resena> Resenas { get; set; } = [];
}
