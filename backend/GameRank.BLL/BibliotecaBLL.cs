using GameRank.BE;
using GameRank.DAL;
using GameRank.Services;

namespace GameRank.BLL;

public class BibliotecaBLL
{
    private readonly BibliotecaDAL _dal;

    public BibliotecaBLL(BibliotecaDAL dal)
    {
        _dal = dal;
    }

    public Task<List<EstadoLista>> ListarEstados() => _dal.ListarEstados();

    public Task Asignar(int usuarioId, int juegoId, int? estadoListaId)
    {
        if (estadoListaId is null or < 1 or > byte.MaxValue)
            throw new AppException(400, "Estado de lista no válido.");

        return _dal.Asignar(usuarioId, juegoId, (byte)estadoListaId.Value);
    }

    public Task Quitar(int usuarioId, int juegoId) => _dal.Quitar(usuarioId, juegoId);

    public Task<List<ItemBiblioteca>> Listar(int usuarioId, int? estadoListaId)
    {
        if (estadoListaId is < 1 or > byte.MaxValue)
            throw new AppException(400, "Estado de lista no válido.");

        return _dal.Listar(usuarioId, (byte?)estadoListaId);
    }
}
