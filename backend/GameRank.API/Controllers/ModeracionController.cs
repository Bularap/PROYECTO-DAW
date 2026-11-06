using GameRank.API.Modelos;
using GameRank.API.Seguridad;
using GameRank.BE;
using GameRank.BLL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameRank.API.Controllers;

[ApiController]
[Authorize(Roles = Roles.Administrador + "," + Roles.SuperAdministrador)]
[Route("api/moderacion")]
public class ModeracionController : ControllerBase
{
    private readonly ModeracionBLL _moderacion;

    public ModeracionController(ModeracionBLL moderacion)
    {
        _moderacion = moderacion;
    }

    [HttpGet("resenas")]
    public async Task<IActionResult> Resenas(string? filtro, int pagina = 1, int tamanioPagina = 20)
    {
        return Ok(await _moderacion.ListarResenas(filtro, pagina, tamanioPagina));
    }

    [HttpPut("resenas/{id:int}")]
    public async Task<IActionResult> CambiarVisibilidad(int id, ModeracionPedido pedido)
    {
        await _moderacion.CambiarVisibilidad(User.Id(), id, pedido.Accion, pedido.Motivo);
        return NoContent();
    }

    [HttpDelete("resenas/{id:int}")]
    public async Task<IActionResult> EliminarResena(int id, string? motivo)
    {
        await _moderacion.EliminarResena(User.Id(), id, motivo);
        return NoContent();
    }

    [HttpPut("usuarios/{id:int}/bloqueo")]
    public async Task<IActionResult> CambiarBloqueo(int id, BloqueoPedido pedido)
    {
        await _moderacion.CambiarBloqueo(User.Id(), id, pedido.Bloquear, pedido.Motivo);
        return NoContent();
    }

    [HttpGet("usuarios-bloqueados")]
    public async Task<IActionResult> UsuariosBloqueados(int pagina = 1, int tamanioPagina = 20)
    {
        return Ok(await _moderacion.ListarBloqueados(pagina, tamanioPagina));
    }
}
