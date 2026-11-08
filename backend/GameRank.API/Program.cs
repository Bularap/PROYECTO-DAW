using GameRank.API;
using GameRank.API.Seguridad;
using GameRank.BLL;
using GameRank.Services;
using GameRank.Services.Steam;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(opciones =>
        opciones.InvalidModelStateResponseFactory = _ =>
            new BadRequestObjectResult(new { mensaje = "La solicitud no es válida." }));

var steam = builder.Configuration.GetSection("Steam").Get<ConfiguracionSteam>() ?? new ConfiguracionSteam();
var respaldo = builder.Configuration.GetSection("Respaldo").Get<ConfiguracionRespaldo>() ?? new ConfiguracionRespaldo();
builder.Services.AgregarCapas(builder.Configuration.GetConnectionString("GameRank")!, steam, respaldo);
builder.Services.AddHostedService<SincronizacionProgramada>();
builder.Services.AddSingleton(new AvatarServicio(builder.Configuration["Avatares:BaseUrl"] ?? "http://localhost:5251"));

builder.Services.AddAuthentication(TokenAuthHandler.Esquema)
    .AddScheme<AuthenticationSchemeOptions, TokenAuthHandler>(TokenAuthHandler.Esquema, null);
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(opciones =>
{
    opciones.AddPolicy("cambio-password", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? contexto.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    opciones.OnRejected = async (contexto, cancelacion) =>
    {
        contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
            contexto.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(espera.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await contexto.HttpContext.Response.WriteAsJsonAsync(new { mensaje = "Demasiados intentos. Espere un minuto antes de volver a cambiar la contraseña." }, cancelacion);
    };
});

var origenes = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? [];
builder.Services.AddCors(opciones =>
    opciones.AddDefaultPolicy(politica =>
        politica.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ManejoErrores>();
app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();
