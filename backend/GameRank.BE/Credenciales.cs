namespace GameRank.BE;

public class Credenciales
{
    public Usuario Usuario { get; set; } = new();
    public string PasswordHash { get; set; } = "";
    public bool Bloqueado { get; set; }
}
