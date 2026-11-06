using GameRank.API;
using GameRank.BLL;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AgregarCapas(builder.Configuration.GetConnectionString("GameRank")!);

var origenes = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? [];
builder.Services.AddCors(opciones =>
    opciones.AddDefaultPolicy(politica =>
        politica.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ManejoErrores>();
app.UseCors();
app.MapControllers();

app.Run();
