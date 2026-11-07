using System.Text.Json.Serialization;

namespace GameRank.BE;

public class Respaldo
{
    public int RespaldoId { get; set; }
    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Formato { get; set; } = "";

    [JsonIgnore]
    public string RutaArchivo { get; set; } = "";

    public string NombreArchivo => Path.GetFileName(RutaArchivo);
    public long? TamanioBytes { get; set; }
    public string? ChecksumSha256 { get; set; }
    public string Estado { get; set; } = "";
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string? MensajeError { get; set; }
}
