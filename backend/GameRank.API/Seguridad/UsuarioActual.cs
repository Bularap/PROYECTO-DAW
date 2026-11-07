using System.Security.Claims;

namespace GameRank.API.Seguridad;

public static class UsuarioActual
{
    public static int Id(this ClaimsPrincipal usuario) =>
        int.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
