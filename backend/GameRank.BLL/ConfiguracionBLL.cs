using GameRank.DAL;
using Microsoft.Extensions.DependencyInjection;

namespace GameRank.BLL;

public static class ConfiguracionBLL
{
    public static IServiceCollection AgregarCapas(this IServiceCollection servicios, string cadena)
    {
        servicios.AddSingleton(new AccesoDatos(cadena));
        servicios.AddScoped<SaludDAL>();
        servicios.AddScoped<SaludBLL>();
        return servicios;
    }
}
