using GameRank.API.Modelos;
using GameRank.API.Seguridad;
using GameRank.BE;
using GameRank.BLL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameRank.API.Controllers;

[ApiController]
[Authorize(Roles = Roles.SuperAdministrador)]
[Route("api/admin/respaldos")]
public class RespaldosController : ControllerBase
{
    private readonly RespaldoBLL _respaldos;

    public RespaldosController(RespaldoBLL respaldos)
    {
        _respaldos = respaldos;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(int pagina = 1, int tamanioPagina = 20)
    {
        return Ok(await _respaldos.Listar(pagina, tamanioPagina));
    }

    [HttpPost]
    public async Task<IActionResult> Generar(RespaldoPedido pedido)
    {
        var respaldo = await _respaldos.Generar(User.Id(), pedido.Tipo);
        return StatusCode(StatusCodes.Status201Created, respaldo);
    }

    [HttpGet("{id:int}/descarga")]
    public async Task<IActionResult> Descargar(int id)
    {
        var respaldo = await _respaldos.ObtenerParaDescarga(id);
        return PhysicalFile(respaldo.RutaArchivo, "application/octet-stream", respaldo.NombreArchivo);
    }

    [HttpPost("{id:int}/restauracion")]
    public async Task<IActionResult> Restaurar(int id, RestauracionPedido pedido)
    {
        return Ok(await _respaldos.Restaurar(User.Id(), id, pedido.Confirmacion));
    }
}
