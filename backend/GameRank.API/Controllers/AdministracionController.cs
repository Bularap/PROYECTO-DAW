using GameRank.API.Modelos;
using GameRank.API.Seguridad;
using GameRank.BE;
using GameRank.BLL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameRank.API.Controllers;

[ApiController]
[Authorize(Roles = Roles.SuperAdministrador)]
[Route("api/admin")]
public class AdministracionController : ControllerBase
{
    private readonly AdministracionBLL _administracion;

    public AdministracionController(AdministracionBLL administracion)
    {
        _administracion = administracion;
    }

    [HttpGet("usuarios")]
    public async Task<IActionResult> Usuarios(string? texto, int pagina = 1, int tamanioPagina = 20)
    {
        return Ok(await _administracion.ListarUsuarios(texto, pagina, tamanioPagina));
    }

    [HttpPut("usuarios/{id:int}/rol")]
    public async Task<IActionResult> AsignarRol(int id, RolPedido pedido)
    {
        await _administracion.AsignarRol(User.Id(), id, pedido.RolId);
        return NoContent();
    }

    [HttpGet("roles")]
    public async Task<IActionResult> RolesDisponibles() => Ok(await _administracion.ListarRoles());

    [HttpGet("auditoria")]
    public async Task<IActionResult> Auditoria(int pagina = 1, int tamanioPagina = 20)
    {
        return Ok(await _administracion.ListarAuditoria(pagina, tamanioPagina));
    }
}
