using System.Text.Json.Serialization;

namespace GameRank.BE;

public class JuegoFicha : JuegoResumen
{
    public string? Sinopsis { get; set; }
    public byte? Metacritic { get; set; }

    [JsonIgnore]
    public DateTime FechaSincronizacion { get; set; }

    public List<ItemCatalogo> Generos { get; set; } = [];
    public List<ItemCatalogo> Plataformas { get; set; } = [];
    public List<ItemCatalogo> Desarrolladoras { get; set; } = [];
}
