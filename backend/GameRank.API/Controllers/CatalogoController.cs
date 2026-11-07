using GameRank.BLL;
using Microsoft.AspNetCore.Mvc;

namespace GameRank.API.Controllers;

[ApiController]
[Route("api")]
public class CatalogoController : ControllerBase
{
    private readonly CatalogoBLL _catalogo;
    private readonly BibliotecaBLL _biblioteca;

    public CatalogoController(CatalogoBLL catalogo, BibliotecaBLL biblioteca)
    {
        _catalogo = catalogo;
        _biblioteca = biblioteca;
    }

    [HttpGet("generos")]
    public async Task<IActionResult> Generos() => Ok(await _catalogo.ListarGeneros());

    [HttpGet("plataformas")]
    public async Task<IActionResult> Plataformas() => Ok(await _catalogo.ListarPlataformas());

    [HttpGet("estados-lista")]
    public async Task<IActionResult> EstadosLista() => Ok(await _biblioteca.ListarEstados());
}
