using GameRank.BE;
using GameRank.DAL;
using GameRank.Services;

namespace GameRank.BLL;

public class AdministracionBLL
{
    private readonly AdministracionDAL _dal;

    public AdministracionBLL(AdministracionDAL dal)
    {
        _dal = dal;
    }

    public Task<ListaPaginada<UsuarioAdmin>> ListarUsuarios(string? texto, int pagina, int tamanioPagina)
    {
        var limpio = texto?.Trim();
        if (limpio?.Length > 100)
            throw new AppException(400, "La búsqueda no puede superar los 100 caracteres.");

        Paginacion.Validar(pagina, tamanioPagina);
        return _dal.ListarUsuarios(string.IsNullOrEmpty(limpio) ? null : limpio, false, pagina, tamanioPagina);
    }

    public Task<List<Rol>> ListarRoles() => _dal.ListarRoles();

    public Task AsignarRol(int superAdminId, int usuarioId, int? rolId)
    {
        if (rolId is null or < 1 or > byte.MaxValue)
            throw new AppException(400, "El rol indicado no existe.");

        return _dal.AsignarRol(superAdminId, usuarioId, (byte)rolId.Value);
    }

    public Task<ListaPaginada<RegistroAuditoria>> ListarAuditoria(int pagina, int tamanioPagina)
    {
        Paginacion.Validar(pagina, tamanioPagina);
        return _dal.ListarAuditoria(pagina, tamanioPagina);
    }
}
