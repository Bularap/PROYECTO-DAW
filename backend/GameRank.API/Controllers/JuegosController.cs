using GameRank.BE;
using GameRank.BLL;
using Microsoft.AspNetCore.Mvc;

namespace GameRank.API.Controllers;

[ApiController]
[Route("api/juegos")]
public class JuegosController : ControllerBase
{
    private readonly CatalogoBLL _catalogo;

    public JuegosController(CatalogoBLL catalogo)
    {
        _catalogo = catalogo;
    }

    [HttpGet]
    public async Task<IActionResult> Buscar([FromQuery] FiltroCatalogo filtro)
    {
        return Ok(await _catalogo.Buscar(filtro));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Ficha(int id)
    {
        return Ok(await _catalogo.ObtenerFicha(id));
    }
}
