namespace GameRank.Services.Rawg;

public class ConfiguracionRawg
{
    public string Modo { get; set; } = "Mock";
    public string BaseUrl { get; set; } = "https://api.rawg.io/api";
    public string ApiKey { get; set; } = "";
    public double TimeoutSegundos { get; set; } = 1.5;
    public int IntervaloHoras { get; set; } = 24;
    public int PaginasPorCiclo { get; set; } = 3;
    public int TamanioLote { get; set; } = 40;
    public int MaxReintentos { get; set; } = 3;
}
