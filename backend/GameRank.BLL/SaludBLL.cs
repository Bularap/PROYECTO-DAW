using GameRank.BE;
using GameRank.DAL;

namespace GameRank.BLL;

public class SaludBLL
{
    private readonly SaludDAL _dal;

    public SaludBLL(SaludDAL dal)
    {
        _dal = dal;
    }

    public Task<EstadoBase> ObtenerEstado() => _dal.ObtenerEstado();
}
