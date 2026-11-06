using GameRank.Services;

namespace GameRank.API;

public class ManejoErrores
{
    private readonly RequestDelegate _siguiente;
    private readonly ILogger<ManejoErrores> _log;

    public ManejoErrores(RequestDelegate siguiente, ILogger<ManejoErrores> log)
    {
        _siguiente = siguiente;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _siguiente(contexto);
        }
        catch (AppException ex)
        {
            contexto.Response.StatusCode = ex.CodigoHttp;
            await contexto.Response.WriteAsJsonAsync(new { mensaje = ex.Message });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error no controlado en {Ruta}", contexto.Request.Path);
            contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await contexto.Response.WriteAsJsonAsync(new { mensaje = "Ocurrió un error en el servidor." });
        }
    }
}
