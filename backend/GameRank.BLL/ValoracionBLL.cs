using GameRank.BE;
using GameRank.DAL;
using GameRank.Services;

namespace GameRank.BLL;

public class ValoracionBLL
{
    private const int LongitudMinima = 10;
    private const int LongitudMaxima = 2000;
    private const int LongitudComentario = 300;
    private static readonly string[] MotivosValidos = ["SPAM", "OFENSIVO", "SPOILER", "OTRO"];

    private readonly ValoracionDAL _dal;

    public ValoracionBLL(ValoracionDAL dal)
    {
        _dal = dal;
    }

    public async Task<MetricasJuego> Calificar(int usuarioId, int juegoId, int? puntuacion)
    {
        if (puntuacion is null or < 1 or > 5)
            throw new AppException(400, "La puntuación debe estar entre 1 y 5.");

        return await _dal.Calificar(usuarioId, juegoId, (byte)puntuacion.Value)
            ?? throw new AppException(404, "Juego no encontrado.");
    }

    public async Task<MetricasJuego> EliminarCalificacion(int usuarioId, int juegoId)
    {
        return await _dal.EliminarCalificacion(usuarioId, juegoId)
            ?? throw new AppException(404, "Juego no encontrado.");
    }

    public Task<Resena> CrearResena(int usuarioId, int juegoId, string? contenido) =>
        _dal.CrearResena(usuarioId, juegoId, ValidarContenido(contenido));

    public Task<Resena> EditarResena(int usuarioId, int resenaId, string? contenido) =>
        _dal.EditarResena(usuarioId, resenaId, ValidarContenido(contenido));

    public Task EliminarResena(int usuarioId, int resenaId) => _dal.EliminarResena(usuarioId, resenaId);

    public Task ReportarResena(int usuarioId, int resenaId, string? motivo, string? comentario)
    {
        var motivoNormalizado = motivo?.Trim().ToUpperInvariant() ?? "";
        if (!MotivosValidos.Contains(motivoNormalizado))
            throw new AppException(400, "El motivo del reporte no es válido.");

        var comentarioLimpio = TextoServicio.Sanitizar(comentario);
        if (comentarioLimpio.Length > LongitudComentario)
            throw new AppException(400, $"El comentario no puede superar los {LongitudComentario} caracteres.");

        return _dal.ReportarResena(usuarioId, resenaId, motivoNormalizado, comentarioLimpio.Length == 0 ? null : comentarioLimpio);
    }

    public Task<ResultadoFeed> ObtenerFeed(int juegoId, int pagina, int tamanioPagina)
    {
        if (pagina < 1)
            throw new AppException(400, "La página debe ser mayor o igual a 1.");
        if (tamanioPagina < 1 || tamanioPagina > 50)
            throw new AppException(400, "El tamaño de página debe estar entre 1 y 50.");

        return _dal.ObtenerFeed(juegoId, pagina, tamanioPagina);
    }

    public Task<EstadoUsuarioJuego> ObtenerEstadoUsuario(int usuarioId, int juegoId) =>
        _dal.ObtenerEstadoUsuario(usuarioId, juegoId);

    private static string ValidarContenido(string? contenido)
    {
        var limpio = TextoServicio.Sanitizar(contenido);
        if (limpio.Length < LongitudMinima || limpio.Length > LongitudMaxima)
            throw new AppException(400, $"La reseña debe tener entre {LongitudMinima} y {LongitudMaxima} caracteres.");

        return limpio;
    }
}
