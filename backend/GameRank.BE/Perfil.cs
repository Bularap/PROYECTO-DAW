namespace GameRank.BE;

public class Perfil
{
    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public DateTime FechaRegistro { get; set; }
    public decimal? PromedioOtorgado { get; set; }
    public int CantidadCalificaciones { get; set; }
    public int CantidadResenas { get; set; }
    public List<CantidadLista> Listas { get; set; } = [];
    public List<CalificacionPerfil> Calificaciones { get; set; } = [];
    public List<ResenaPerfil> Resenas { get; set; } = [];
}

public class CantidadLista
{
    public byte EstadoListaId { get; set; }
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }
}

public class CalificacionPerfil
{
    public int JuegoId { get; set; }
    public string Titulo { get; set; } = "";
    public string? PortadaUrl { get; set; }
    public byte Puntuacion { get; set; }
    public DateTime Fecha { get; set; }
}

public class ResenaPerfil
{
    public int ResenaId { get; set; }
    public int JuegoId { get; set; }
    public string Titulo { get; set; } = "";
    public byte? Puntuacion { get; set; }
    public string Contenido { get; set; } = "";
    public DateTime FechaPublicacion { get; set; }
    public bool Editada { get; set; }
}
