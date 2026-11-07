using System.Security.Claims;
using System.Text.Encodings.Web;
using GameRank.BLL;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace GameRank.API.Seguridad;

public class TokenAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Esquema = "Token";

    private readonly AuthBLL _auth;

    public TokenAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> opciones,
        ILoggerFactory log,
        UrlEncoder encoder,
        AuthBLL auth) : base(opciones, log, encoder)
    {
        _auth = auth;
    }

    public static string? LeerToken(HttpRequest pedido)
    {
        const string prefijo = "Bearer ";
        string? cabecera = pedido.Headers.Authorization;

        if (cabecera == null || !cabecera.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            return null;

        var token = cabecera[prefijo.Length..].Trim();
        return token.Length == 0 ? null : token;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = LeerToken(Request);
        if (token == null)
            return AuthenticateResult.NoResult();

        var usuario = await _auth.ValidarToken(token);
        if (usuario == null)
            return AuthenticateResult.Fail("Token inválido.");

        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, usuario.UsuarioId.ToString()),
            new Claim(ClaimTypes.Name, usuario.NombreUsuario),
            new Claim(ClaimTypes.Role, usuario.Rol)
        ], Esquema);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties propiedades)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(new { mensaje = "Debe iniciar sesión." });
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties propiedades)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Response.WriteAsJsonAsync(new { mensaje = "No tiene permisos para esta acción." });
    }
}
