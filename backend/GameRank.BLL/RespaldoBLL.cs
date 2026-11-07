using System.IO.Compression;
using System.Text;
using GameRank.BE;
using GameRank.DAL;
using GameRank.Services;

namespace GameRank.BLL;

public class RespaldoBLL
{
    private const string Completo = "COMPLETO";
    private const string Diferencial = "DIFERENCIAL";
    private const string Log = "LOG";
    private const string Exportacion = "EXPORTACION";
    private const string TextoConfirmacion = "RESTAURAR";
    private const int TamanioHistorial = 100000;

    private readonly RespaldoDAL _dal;
    private readonly ConfiguracionRespaldo _config;

    public RespaldoBLL(RespaldoDAL dal, ConfiguracionRespaldo config)
    {
        _dal = dal;
        _config = config;
    }

    public Task<ListaPaginada<Respaldo>> Listar(int pagina, int tamanioPagina)
    {
        Paginacion.Validar(pagina, tamanioPagina);
        return _dal.Listar(pagina, tamanioPagina);
    }

    public async Task<Respaldo> Generar(int usuarioId, string? tipo)
    {
        var tipoNormalizado = tipo?.Trim().ToUpperInvariant() ?? "";
        var historial = await ObtenerHistorial();

        switch (tipoNormalizado)
        {
            case Completo:
                await GenerarBackup(usuarioId, Completo);
                break;
            case Diferencial:
            case Log:
                if (!historial.Any(r => r.Tipo == Completo && EstaDisponible(r)))
                    throw new AppException(409, "Primero debe generarse un respaldo completo.");
                await GenerarBackup(usuarioId, tipoNormalizado);
                break;
            case Exportacion:
                await GenerarExportacion(usuarioId);
                break;
            default:
                throw new AppException(400, "El tipo debe ser COMPLETO, DIFERENCIAL, LOG o EXPORTACION.");
        }

        return (await ObtenerHistorial()).First();
    }

    public async Task<Respaldo> ObtenerParaDescarga(int id)
    {
        var respaldo = await Buscar(id);
        if (!EstaDisponible(respaldo))
            throw new AppException(409, "El respaldo no se completó correctamente.");
        if (!ArchivoServicio.PuedeLeerse(respaldo.RutaArchivo))
            throw new AppException(409, "El archivo de respaldo no está disponible.");

        return respaldo;
    }

    public async Task<ResultadoRestauracion> Restaurar(int usuarioId, int id, string? confirmacion)
    {
        if (confirmacion != TextoConfirmacion)
            throw new AppException(400, $"Para restaurar debe enviar la confirmación {TextoConfirmacion}.");

        var historial = await ObtenerHistorial();
        var objetivo = historial.FirstOrDefault(r => r.RespaldoId == id)
            ?? throw new AppException(404, "El respaldo no existe.");

        if (objetivo.Tipo == Exportacion)
            throw new AppException(400, "Una exportación CSV no se puede restaurar.");
        if (!EstaDisponible(objetivo))
            throw new AppException(409, "El respaldo no se completó correctamente.");

        var cadena = ArmarCadena(historial, objetivo);
        foreach (var respaldo in cadena)
            await ValidarIntegridad(respaldo);

        var seguridad = await GenerarBackup(usuarioId, Completo);
        historial = await ObtenerHistorial();

        try
        {
            await _dal.Restaurar(cadena);
        }
        catch (AppException ex)
        {
            await _dal.Restaurar([historial.First(r => r.RespaldoId == seguridad)]);
            await ReponerHistorial(historial);
            throw new AppException(500, "No se pudo restaurar el respaldo y se recuperó el estado anterior. " + ex.Message);
        }

        await ReponerHistorial(historial);
        await _dal.RegistrarRestauracion(usuarioId, id);
        var nuevaBase = await GenerarBackup(usuarioId, Completo);

        return new ResultadoRestauracion
        {
            RespaldoRestauradoId = id,
            RespaldoSeguridadId = seguridad,
            NuevaBaseId = nuevaBase
        };
    }

    private static List<Respaldo> ArmarCadena(List<Respaldo> historial, Respaldo objetivo)
    {
        if (objetivo.Tipo == Completo)
            return [objetivo];

        var anteriores = historial
            .Where(r => r.RespaldoId < objetivo.RespaldoId && EstaDisponible(r))
            .OrderBy(r => r.RespaldoId)
            .ToList();

        var completo = anteriores.LastOrDefault(r => r.Tipo == Completo)
            ?? throw new AppException(409, "No hay un respaldo completo anterior para restaurar.");

        if (objetivo.Tipo == Diferencial)
            return [completo, objetivo];

        var diferencial = anteriores.LastOrDefault(r => r.Tipo == Diferencial && r.RespaldoId > completo.RespaldoId);
        var desde = diferencial?.RespaldoId ?? completo.RespaldoId;
        var logs = anteriores.Where(r => r.Tipo == Log && r.RespaldoId > desde);

        var cadena = new List<Respaldo> { completo };
        if (diferencial != null)
            cadena.Add(diferencial);
        cadena.AddRange(logs);
        cadena.Add(objetivo);
        return cadena;
    }

    private async Task ValidarIntegridad(Respaldo respaldo)
    {
        if (!ArchivoServicio.PuedeLeerse(respaldo.RutaArchivo) && respaldo.ChecksumSha256 != null)
            throw new AppException(409, $"El archivo {respaldo.NombreArchivo} no está disponible.");

        var actual = ArchivoServicio.Sha256(respaldo.RutaArchivo);
        if (respaldo.ChecksumSha256 != null && actual != respaldo.ChecksumSha256)
            throw new AppException(409, $"El archivo {respaldo.NombreArchivo} está dañado. No se realizó la restauración.");

        await _dal.Verificar(respaldo.RutaArchivo);
    }

    private async Task<int> GenerarBackup(int usuarioId, string tipo)
    {
        var directorio = await ResolverDirectorio();
        var ruta = Path.Combine(directorio, NombreArchivo(tipo, tipo == Log ? "trn" : "bak"));
        var id = await _dal.Iniciar(usuarioId, tipo, tipo == Log ? "TRN" : "BAK", ruta);

        try
        {
            await _dal.EjecutarBackup(tipo, ruta);
        }
        catch (AppException ex)
        {
            ArchivoServicio.EliminarSiExiste(ruta);
            await _dal.Finalizar(id, "ERROR", null, null, Recortar(ex.Message));
            throw new AppException(500, "No se pudo generar el respaldo. " + ex.Message);
        }

        await _dal.Finalizar(id, "OK", ArchivoServicio.Tamanio(ruta), ArchivoServicio.Sha256(ruta), null);
        return id;
    }

    private async Task GenerarExportacion(int usuarioId)
    {
        var directorio = await ResolverDirectorio();
        var ruta = Path.Combine(directorio, NombreArchivo(Exportacion, "zip"));
        var id = await _dal.Iniciar(usuarioId, Exportacion, "CSV", ruta);

        try
        {
            await using (var archivo = File.Create(ruta))
            using (var zip = new ZipArchive(archivo, ZipArchiveMode.Create))
            {
                foreach (var tabla in _dal.NombresTablasExportables)
                {
                    await using var entrada = zip.CreateEntry(tabla + ".csv").Open();
                    await using var escritor = new StreamWriter(entrada, new UTF8Encoding(true));
                    await _dal.EscribirTablaCsv(tabla, escritor);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ArchivoServicio.EliminarSiExiste(ruta);
            await _dal.Finalizar(id, "ERROR", null, null, Recortar(ex.Message));
            throw new AppException(500, "No se pudo generar la exportación. " + ex.Message);
        }

        await _dal.Finalizar(id, "OK", ArchivoServicio.Tamanio(ruta), ArchivoServicio.Sha256(ruta), null);
    }

    private async Task<string> ResolverDirectorio()
    {
        var directorio = _config.Directorio;
        if (string.IsNullOrWhiteSpace(directorio))
        {
            var predeterminado = await _dal.ObtenerDirectorioPredeterminado()
                ?? throw new AppException(500, "No hay un directorio de respaldos configurado.");
            directorio = Path.Combine(predeterminado, "GameRank");
        }

        await _dal.CrearDirectorio(directorio);
        return directorio;
    }

    private async Task ReponerHistorial(List<Respaldo> historial)
    {
        foreach (var respaldo in historial.OrderBy(r => r.RespaldoId))
            await _dal.Reponer(respaldo);
    }

    private async Task<Respaldo> Buscar(int id)
    {
        var historial = await ObtenerHistorial();
        return historial.FirstOrDefault(r => r.RespaldoId == id)
            ?? throw new AppException(404, "El respaldo no existe.");
    }

    private async Task<List<Respaldo>> ObtenerHistorial() =>
        (await _dal.Listar(1, TamanioHistorial)).Items;

    private static bool EstaDisponible(Respaldo respaldo) => respaldo.Estado is "OK" or "RESTAURADO";

    private static string NombreArchivo(string tipo, string extension) =>
        $"GameRank_{tipo}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}.{extension}";

    private static string Recortar(string mensaje) => mensaje.Length > 1000 ? mensaje[..1000] : mensaje;
}
