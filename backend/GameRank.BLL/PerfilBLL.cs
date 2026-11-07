using GameRank.BE;
using GameRank.DAL;

namespace GameRank.BLL;

public class PerfilBLL
{
    private readonly PerfilDAL _dal;

    public PerfilBLL(PerfilDAL dal)
    {
        _dal = dal;
    }

    public Task<Perfil> Obtener(int usuarioId) => _dal.Obtener(usuarioId);
}
