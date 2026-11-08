using System.Security.Claims;
using GameRank.API.Modelos;
using GameRank.API.Seguridad;
using GameRank.BE;
using GameRank.BLL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameRank.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthBLL _auth;

    public AuthController(AuthBLL auth)
    {
        _auth = auth;
    }

    [HttpPost("registro")]
    public async Task<IActionResult> Registro(RegistroPedido pedido)
    {
        var sesion = await _auth.Registrar(pedido.Email, pedido.NombreUsuario, pedido.Password, pedido.AceptaPrivacidad, Ip());
        return StatusCode(StatusCodes.Status201Created, sesion);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginPedido pedido)
    {
        return Ok(await _auth.Login(pedido.Email, pedido.Password, Ip()));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var token = TokenAuthHandler.LeerToken(Request);
        if (token != null)
            await _auth.Logout(token);

        return NoContent();
    }

    [Authorize]
    [HttpGet("yo")]
    public IActionResult Yo()
    {
        return Ok(new Usuario
        {
            UsuarioId = User.Id(),
            NombreUsuario = User.Identity!.Name!,
            Rol = User.FindFirstValue(ClaimTypes.Role)!
        });
    }

    [Authorize]
    [HttpGet("cuenta")]
    public async Task<IActionResult> Cuenta()
    {
        return Ok(await _auth.ObtenerCuenta(User.Id()));
    }

    [Authorize]
    [HttpPut("perfil")]
    public async Task<IActionResult> Perfil(PerfilEdicionPedido pedido)
    {
        return Ok(await _auth.ActualizarPerfil(User.Id(), pedido.NombreUsuario, pedido.AvatarId));
    }

    [Authorize]
    [EnableRateLimiting("cambio-password")]
    [HttpPut("password")]
    public async Task<IActionResult> Password(PasswordCambioPedido pedido)
    {
        await _auth.CambiarPassword(User.Id(), pedido.PasswordActual, pedido.PasswordNueva);
        return NoContent();
    }

    [Authorize]
    [HttpPost("baja")]
    public async Task<IActionResult> Baja(BajaPedido pedido)
    {
        await _auth.EliminarCuenta(User.Id(), pedido.Password);
        return NoContent();
    }

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
