using GameRank.API.Modelos;
using GameRank.API.Seguridad;
using GameRank.BLL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameRank.API.Controllers;

[ApiController]
[Route("api/juegos/{juegoId:int}")]
public class JuegoInteraccionController : ControllerBase
{
    private readonly ValoracionBLL _valoracion;
    private readonly BibliotecaBLL _biblioteca;

    public JuegoInteraccionController(ValoracionBLL valoracion, BibliotecaBLL biblioteca)
    {
        _valoracion = valoracion;
        _biblioteca = biblioteca;
    }

    [HttpGet("resenas")]
    public async Task<IActionResult> Feed(int juegoId, int pagina = 1, int tamanioPagina = 10)
    {
        return Ok(await _valoracion.ObtenerFeed(juegoId, pagina, tamanioPagina));
    }

    [Authorize]
    [HttpPost("resenas")]
    public async Task<IActionResult> CrearResena(int juegoId, ResenaPedido pedido)
    {
        var resena = await _valoracion.CrearResena(User.Id(), juegoId, pedido.Contenido);
        return StatusCode(StatusCodes.Status201Created, resena);
    }

    [Authorize]
    [HttpPut("calificacion")]
    public async Task<IActionResult> Calificar(int juegoId, CalificacionPedido pedido)
    {
        return Ok(await _valoracion.Calificar(User.Id(), juegoId, pedido.Puntuacion));
    }

    [Authorize]
    [HttpDelete("calificacion")]
    public async Task<IActionResult> EliminarCalificacion(int juegoId)
    {
        return Ok(await _valoracion.EliminarCalificacion(User.Id(), juegoId));
    }

    [Authorize]
    [HttpPut("biblioteca")]
    public async Task<IActionResult> AsignarBiblioteca(int juegoId, BibliotecaPedido pedido)
    {
        await _biblioteca.Asignar(User.Id(), juegoId, pedido.EstadoListaId);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("biblioteca")]
    public async Task<IActionResult> QuitarBiblioteca(int juegoId)
    {
        await _biblioteca.Quitar(User.Id(), juegoId);
        return NoContent();
    }

    [Authorize]
    [HttpGet("mi-estado")]
    public async Task<IActionResult> MiEstado(int juegoId)
    {
        return Ok(await _valoracion.ObtenerEstadoUsuario(User.Id(), juegoId));
    }
}
