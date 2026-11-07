namespace GameRank.BE;

public class UsuarioAdmin
{
    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string? Email { get; set; }
    public byte RolId { get; set; }
    public string Rol { get; set; } = "";
    public bool Bloqueado { get; set; }
    public DateTime FechaRegistro { get; set; }
    public DateTime? UltimoLogin { get; set; }
}
