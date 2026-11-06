namespace GameRank.BE;

public class EstadoUsuarioJuego
{
    public byte? Puntuacion { get; set; }
    public byte? EstadoListaId { get; set; }
    public int? ResenaId { get; set; }
    public string? ResenaContenido { get; set; }
}
