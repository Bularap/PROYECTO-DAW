namespace GameRank.BE;

public class JuegoResumen
{
    public int JuegoId { get; set; }
    public int SteamAppId { get; set; }
    public string Titulo { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? PortadaUrl { get; set; }
    public DateOnly? FechaLanzamiento { get; set; }
    public decimal? PromedioCalificacion { get; set; }
    public int CantidadCalificaciones { get; set; }
}
