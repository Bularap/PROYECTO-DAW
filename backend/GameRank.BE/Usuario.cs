namespace GameRank.BE;

public class Usuario
{
    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string Rol { get; set; } = Roles.Jugador;
}
