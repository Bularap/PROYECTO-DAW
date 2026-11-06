using GameRank.API.Modelos;
using GameRank.API.Seguridad;
using GameRank.BLL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameRank.API.Controllers;

[ApiController]
[Authorize]
[Route("api/resenas/{id:int}")]
public class ResenasController : ControllerBase
{
    private readonly ValoracionBLL _valoracion;

    public ResenasController(ValoracionBLL valoracion)
    {
        _valoracion = valoracion;
    }

    [HttpPut]
    public async Task<IActionResult> Editar(int id, ResenaPedido pedido)
    {
        return Ok(await _valoracion.EditarResena(User.Id(), id, pedido.Contenido));
    }

    [HttpDelete]
    public async Task<IActionResult> Eliminar(int id)
    {
        await _valoracion.EliminarResena(User.Id(), id);
        return NoContent();
    }

    [HttpPost("reportes")]
    public async Task<IActionResult> Reportar(int id, ReportePedido pedido)
    {
        await _valoracion.ReportarResena(User.Id(), id, pedido.Motivo, pedido.Comentario);
        return NoContent();
    }
}
