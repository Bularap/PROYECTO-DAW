using GameRank.BLL;
using GameRank.Services.Steam;

namespace GameRank.API;

public class SincronizacionProgramada : BackgroundService
{
    private readonly IServiceScopeFactory _ambitos;
    private readonly ConfiguracionSteam _config;
    private readonly ILogger<SincronizacionProgramada> _log;

    public SincronizacionProgramada(IServiceScopeFactory ambitos, ConfiguracionSteam config, ILogger<SincronizacionProgramada> log)
    {
        _ambitos = ambitos;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        if (_config.IntervaloHoras <= 0)
            return;

        using var temporizador = new PeriodicTimer(TimeSpan.FromHours(_config.IntervaloHoras));

        do
        {
            await Ejecutar();
        }
        while (await EsperarSiguiente(temporizador, cancelacion));
    }

    private async Task Ejecutar()
    {
        try
        {
            await using var ambito = _ambitos.CreateAsyncScope();
            var sincronizacion = ambito.ServiceProvider.GetRequiredService<SincronizacionBLL>();

            await sincronizacion.Sincronizar(SincronizacionBLL.OrigenProgramada, null, _config.MaxReintentos);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falló la sincronización programada.");
        }
    }

    private static async Task<bool> EsperarSiguiente(PeriodicTimer temporizador, CancellationToken cancelacion)
    {
        try
        {
            return await temporizador.WaitForNextTickAsync(cancelacion);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
