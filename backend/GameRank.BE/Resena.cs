namespace GameRank.BE;

public class Resena
{
    public int ResenaId { get; set; }
    public int JuegoId { get; set; }
    public string? Titulo { get; set; }
    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public byte? Puntuacion { get; set; }
    public string Contenido { get; set; } = "";
    public DateTime FechaPublicacion { get; set; }
    public bool Editada { get; set; }
}
