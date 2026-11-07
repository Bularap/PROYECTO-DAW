namespace GameRank.Services.Steam;

public class ConfiguracionSteam
{
    public string Modo { get; set; } = "Real";
    public string TiendaUrl { get; set; } = "https://store.steampowered.com/api";
    public double TimeoutSegundos { get; set; } = 1.5;
    public int IntervaloHoras { get; set; } = 24;
    public int MaxReintentos { get; set; } = 3;
    public int MaxJuegosPorCiclo { get; set; } = 40;
}
