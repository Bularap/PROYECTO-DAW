namespace GameRank.BE;

public class ItemBiblioteca
{
    public int JuegoId { get; set; }
    public string Titulo { get; set; } = "";
    public string? PortadaUrl { get; set; }
    public byte EstadoListaId { get; set; }
    public string Estado { get; set; } = "";
    public DateTime Fecha { get; set; }
}
