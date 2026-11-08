namespace GameRank.BE;

public class Cuenta
{
    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string Email { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public IReadOnlyDictionary<string, string> Avatares { get; set; } = new Dictionary<string, string>();
}
