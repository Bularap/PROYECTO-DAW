namespace GameRank.BE;

public class RegistroAuditoria
{
    public long AuditoriaId { get; set; }
    public int UsuarioActorId { get; set; }
    public string Actor { get; set; } = "";
    public string Accion { get; set; } = "";
    public string EntidadTipo { get; set; } = "";
    public int EntidadId { get; set; }
    public string? Motivo { get; set; }
    public string? Detalle { get; set; }
    public DateTime Fecha { get; set; }
}
