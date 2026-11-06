using GameRank.Services;

namespace GameRank.BLL;

public static class Paginacion
{
    public static void Validar(int pagina, int tamanioPagina)
    {
        if (pagina < 1)
            throw new AppException(400, "La página debe ser mayor o igual a 1.");
        if (tamanioPagina < 1 || tamanioPagina > 100)
            throw new AppException(400, "El tamaño de página debe estar entre 1 y 100.");
    }
}
