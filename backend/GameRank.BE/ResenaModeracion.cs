namespace GameRank.BE;

public class ResenaModeracion
{
    public int ResenaId { get; set; }
    public int JuegoId { get; set; }
    public string Titulo { get; set; } = "";
    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = "";
    public bool AutorBloqueado { get; set; }
    public string Contenido { get; set; } = "";
    public string Estado { get; set; } = "";
    public DateTime FechaPublicacion { get; set; }
    public int CantidadReportes { get; set; }
    public DateTime? UltimoReporte { get; set; }
    public string? Motivos { get; set; }
}
