using System.Text.Json.Serialization;

namespace GameRank.API.Modelos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class PasswordCambioPedido
{
    public string? PasswordActual { get; set; }
    public string? PasswordNueva { get; set; }
}
