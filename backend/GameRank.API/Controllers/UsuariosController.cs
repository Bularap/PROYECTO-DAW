using GameRank.BLL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameRank.API.Controllers;

[ApiController]
[Authorize]
[Route("api/usuarios/{id:int}")]
public class UsuariosController : ControllerBase
{
    private readonly PerfilBLL _perfil;
    private readonly BibliotecaBLL _biblioteca;

    public UsuariosController(PerfilBLL perfil, BibliotecaBLL biblioteca)
    {
        _perfil = perfil;
        _biblioteca = biblioteca;
    }

    [HttpGet]
    public async Task<IActionResult> Perfil(int id)
    {
        return Ok(await _perfil.Obtener(id));
    }

    [HttpGet("biblioteca")]
    public async Task<IActionResult> Biblioteca(int id, int? estadoListaId)
    {
        return Ok(await _biblioteca.Listar(id, estadoListaId));
    }
}
