using System.Text.Json.Serialization;

namespace GameRank.API.Modelos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class PerfilEdicionPedido
{
    public string? NombreUsuario { get; set; }
    public string? AvatarId { get; set; }
}
