namespace GameRank.BE;

public class FiltroCatalogo
{
    public string? Texto { get; set; }
    public string? Generos { get; set; }
    public string? Plataformas { get; set; }
    public decimal? PuntuacionMin { get; set; }
    public decimal? PuntuacionMax { get; set; }
    public string? Orden { get; set; }
    public int Pagina { get; set; } = 1;
    public int TamanioPagina { get; set; } = 24;
}
