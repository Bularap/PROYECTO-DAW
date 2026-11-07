using GameRank.BE;
using GameRank.DAL;
using GameRank.Services;

namespace GameRank.BLL;

public class ModeracionBLL
{
    private const int LongitudMotivo = 300;
    private static readonly string[] FiltrosValidos = ["REPORTADAS", "RECIENTES", "OCULTAS"];

    private readonly ModeracionDAL _dal;
    private readonly AdministracionDAL _administracion;

    public ModeracionBLL(ModeracionDAL dal, AdministracionDAL administracion)
    {
        _dal = dal;
        _administracion = administracion;
    }

    public Task<ListaPaginada<ResenaModeracion>> ListarResenas(string? filtro, int pagina, int tamanioPagina)
    {
        var filtroNormalizado = string.IsNullOrWhiteSpace(filtro) ? "REPORTADAS" : filtro.Trim().ToUpperInvariant();
        if (!FiltrosValidos.Contains(filtroNormalizado))
            throw new AppException(400, "El filtro indicado no es válido.");

        Paginacion.Validar(pagina, tamanioPagina);
        return _dal.ListarResenas(filtroNormalizado, pagina, tamanioPagina);
    }

    public Task CambiarVisibilidad(int adminId, int resenaId, string? accion, string? motivo)
    {
        var accionNormalizada = accion?.Trim().ToUpperInvariant();

        return accionNormalizada switch
        {
            "OCULTAR" => _dal.Moderar(adminId, resenaId, "OCULTAR", MotivoObligatorio(motivo)),
            "MOSTRAR" => _dal.Moderar(adminId, resenaId, "MOSTRAR", MotivoOpcional(motivo)),
            _ => throw new AppException(400, "La acción debe ser OCULTAR o MOSTRAR.")
        };
    }

    public Task EliminarResena(int adminId, int resenaId, string? motivo) =>
        _dal.Moderar(adminId, resenaId, "ELIMINAR", MotivoObligatorio(motivo));

    public Task CambiarBloqueo(int adminId, int usuarioId, bool? bloquear, string? motivo)
    {
        if (bloquear == null)
            throw new AppException(400, "Debe indicar si la cuenta se bloquea o se desbloquea.");

        var motivoValidado = bloquear.Value ? MotivoObligatorio(motivo) : MotivoOpcional(motivo);
        return _dal.CambiarBloqueo(adminId, usuarioId, bloquear.Value, motivoValidado);
    }

    public async Task<ListaPaginada<UsuarioAdmin>> ListarBloqueados(int pagina, int tamanioPagina)
    {
        Paginacion.Validar(pagina, tamanioPagina);

        var resultado = await _administracion.ListarUsuarios(null, true, pagina, tamanioPagina);
        foreach (var usuario in resultado.Items)
            usuario.Email = null;

        return resultado;
    }

    private static string MotivoObligatorio(string? motivo) =>
        MotivoOpcional(motivo) ?? throw new AppException(400, "Debe indicar el motivo de la acción.");

    private static string? MotivoOpcional(string? motivo)
    {
        var limpio = TextoServicio.Sanitizar(motivo);
        if (limpio.Length > LongitudMotivo)
            throw new AppException(400, $"El motivo no puede superar los {LongitudMotivo} caracteres.");

        return limpio.Length == 0 ? null : limpio;
    }
}
