using GameRank.BLL;
using Microsoft.AspNetCore.Mvc;

namespace GameRank.API.Controllers;

[ApiController]
[Route("api")]
public class CatalogoController : ControllerBase
{
    private readonly CatalogoBLL _catalogo;

    public CatalogoController(CatalogoBLL catalogo)
    {
        _catalogo = catalogo;
    }

    [HttpGet("generos")]
    public async Task<IActionResult> Generos() => Ok(await _catalogo.ListarGeneros());

    [HttpGet("plataformas")]
    public async Task<IActionResult> Plataformas() => Ok(await _catalogo.ListarPlataformas());
}
