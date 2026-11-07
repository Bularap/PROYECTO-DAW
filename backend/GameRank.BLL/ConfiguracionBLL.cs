using GameRank.DAL;
using GameRank.Services;
using GameRank.Services.Steam;
using Microsoft.Extensions.DependencyInjection;

namespace GameRank.BLL;

public static class ConfiguracionBLL
{
    public static IServiceCollection AgregarCapas(
        this IServiceCollection servicios,
        string cadena,
        ConfiguracionSteam steam,
        ConfiguracionRespaldo respaldo)
    {
        servicios.AddSingleton(new AccesoDatos(cadena));
        servicios.AddSingleton(steam);
        servicios.AddSingleton(respaldo);

        if (steam.Modo.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            servicios.AddSingleton<ISteamCliente>(new SteamMock());
        else
            servicios.AddSingleton<ISteamCliente>(new SteamHttp(steam));

        servicios.AddScoped<SaludDAL>();
        servicios.AddScoped<SaludBLL>();
        servicios.AddScoped<UsuarioDAL>();
        servicios.AddScoped<AuthBLL>();
        servicios.AddScoped<CatalogoDAL>();
        servicios.AddScoped<SincronizacionDAL>();
        servicios.AddScoped<SincronizacionBLL>();
        servicios.AddScoped<CatalogoBLL>();
        servicios.AddScoped<ValoracionDAL>();
        servicios.AddScoped<ValoracionBLL>();
        servicios.AddScoped<BibliotecaDAL>();
        servicios.AddScoped<BibliotecaBLL>();
        servicios.AddScoped<PerfilDAL>();
        servicios.AddScoped<PerfilBLL>();
        servicios.AddScoped<ModeracionDAL>();
        servicios.AddScoped<ModeracionBLL>();
        servicios.AddScoped<AdministracionDAL>();
        servicios.AddScoped<AdministracionBLL>();
        servicios.AddScoped<RespaldoDAL>();
        servicios.AddScoped<RespaldoBLL>();
        return servicios;
    }
}
