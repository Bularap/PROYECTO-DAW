using GameRank.BLL;
using Microsoft.AspNetCore.Mvc;

namespace GameRank.API.Controllers;

[ApiController]
[Route("api/salud")]
public class SaludController : ControllerBase
{
    private readonly SaludBLL _bll;

    public SaludController(SaludBLL bll)
    {
        _bll = bll;
    }

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _bll.ObtenerEstado());
}
