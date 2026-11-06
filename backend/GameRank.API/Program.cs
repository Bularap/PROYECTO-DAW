using GameRank.API;
using GameRank.API.Seguridad;
using GameRank.BLL;
using GameRank.Services.Rawg;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(opciones =>
        opciones.InvalidModelStateResponseFactory = _ =>
            new BadRequestObjectResult(new { mensaje = "La solicitud no es válida." }));

var rawg = builder.Configuration.GetSection("Rawg").Get<ConfiguracionRawg>() ?? new ConfiguracionRawg();
builder.Services.AgregarCapas(builder.Configuration.GetConnectionString("GameRank")!, rawg);
builder.Services.AddHostedService<SincronizacionProgramada>();

builder.Services.AddAuthentication(TokenAuthHandler.Esquema)
    .AddScheme<AuthenticationSchemeOptions, TokenAuthHandler>(TokenAuthHandler.Esquema, null);
builder.Services.AddAuthorization();

var origenes = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? [];
builder.Services.AddCors(opciones =>
    opciones.AddDefaultPolicy(politica =>
        politica.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ManejoErrores>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
