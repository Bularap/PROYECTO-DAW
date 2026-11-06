namespace GameRank.BE;

public class ListaPaginada<T>
{
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanioPagina { get; set; }
    public List<T> Items { get; set; } = [];
}
