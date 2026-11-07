using GameRank.BLL;
using Microsoft.AspNetCore.Mvc;

namespace GameRank.API.Controllers;

[ApiController]
[Route("api")]
public class CatalogoController : ControllerBase
{
    private readonly CatalogoBLL _catalogo;
    private readonly BibliotecaBLL _biblioteca;
    private readonly ValoracionBLL _valoracion;

    public CatalogoController(CatalogoBLL catalogo, BibliotecaBLL biblioteca, ValoracionBLL valoracion)
    {
        _catalogo = catalogo;
        _biblioteca = biblioteca;
        _valoracion = valoracion;
    }

    [HttpGet("generos")]
    public async Task<IActionResult> Generos() => Ok(await _catalogo.ListarGeneros());

    [HttpGet("plataformas")]
    public async Task<IActionResult> Plataformas() => Ok(await _catalogo.ListarPlataformas());

    [HttpGet("resenas")]
    public async Task<IActionResult> ResenasRecientes(int pagina = 1, int tamanioPagina = 10)
    {
        return Ok(await _valoracion.ObtenerRecientes(pagina, tamanioPagina));
    }

    [HttpGet("estados-lista")]
    public async Task<IActionResult> EstadosLista() => Ok(await _biblioteca.ListarEstados());
}
