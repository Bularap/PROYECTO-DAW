using GameRank.DAL;
using GameRank.Services.Rawg;
using Microsoft.Extensions.DependencyInjection;

namespace GameRank.BLL;

public static class ConfiguracionBLL
{
    public static IServiceCollection AgregarCapas(this IServiceCollection servicios, string cadena, ConfiguracionRawg rawg)
    {
        servicios.AddSingleton(new AccesoDatos(cadena));
        servicios.AddSingleton(rawg);

        if (rawg.Modo.Equals("Real", StringComparison.OrdinalIgnoreCase))
            servicios.AddSingleton<IRawgCliente>(new RawgHttp(rawg));
        else
            servicios.AddSingleton<IRawgCliente>(new RawgMock());

        servicios.AddScoped<SaludDAL>();
        servicios.AddScoped<SaludBLL>();
        servicios.AddScoped<UsuarioDAL>();
        servicios.AddScoped<AuthBLL>();
        servicios.AddScoped<CatalogoDAL>();
        servicios.AddScoped<SincronizacionDAL>();
        servicios.AddScoped<SincronizacionBLL>();
        servicios.AddScoped<CatalogoBLL>();
        return servicios;
    }
}
