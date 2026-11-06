namespace GameRank.API.Modelos;

public class RegistroPedido
{
    public string? Email { get; set; }
    public string? NombreUsuario { get; set; }
    public string? Password { get; set; }
    public bool AceptaPrivacidad { get; set; }
}
