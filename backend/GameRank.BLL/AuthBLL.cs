using System.Net.Mail;
using System.Text;
using GameRank.BE;
using GameRank.DAL;
using GameRank.Services;

namespace GameRank.BLL;

public class AuthBLL
{
    private const string VersionPolitica = "1.0";

    private readonly UsuarioDAL _dal;
    private readonly AvatarServicio _avatares;

    public AuthBLL(UsuarioDAL dal, AvatarServicio avatares)
    {
        _dal = dal;
        _avatares = avatares;
    }

    public async Task<SesionIniciada> Registrar(string? email, string? nombreUsuario, string? password, bool aceptaPrivacidad, string? ip)
    {
        var correo = email?.Trim() ?? "";
        var nombre = nombreUsuario?.Trim() ?? "";
        var clave = password ?? "";

        if (!EsCorreoValido(correo))
            throw new AppException(400, "El correo no tiene un formato válido.");
        if (nombre.Length < 3 || nombre.Length > 30)
            throw new AppException(400, "El nombre de usuario debe tener entre 3 y 30 caracteres.");
        if (!EsPasswordValida(clave))
            throw new AppException(400, "La contraseña debe tener entre 8 y 72 caracteres, con al menos una letra y un número.");
        if (!aceptaPrivacidad)
            throw new AppException(400, "Debe aceptar la Política de Privacidad (Ley 25.326).");

        var hash = HashServicio.Generar(clave);
        var usuario = await _dal.Registrar(correo, nombre, hash, VersionPolitica);
        return await IniciarSesion(usuario, hash, ip);
    }

    public async Task<SesionIniciada> Login(string? email, string? password, string? ip)
    {
        var correo = email?.Trim() ?? "";
        var clave = password ?? "";

        if (correo.Length == 0 || clave.Length == 0)
            throw new AppException(400, "Debe completar el correo y la contraseña.");

        var credenciales = await _dal.ObtenerCredenciales(correo);
        if (!HashServicio.Verificar(clave, credenciales?.PasswordHash))
            throw new AppException(401, "Correo o contraseña incorrectos.");
        if (credenciales!.Bloqueado)
            throw new AppException(403, "La cuenta se encuentra suspendida.");

        return await IniciarSesion(credenciales.Usuario, credenciales.PasswordHash, ip);
    }

    public Task<Usuario?> ValidarToken(string token) => _dal.ValidarSesion(TokenServicio.Hashear(token));

    public Task Logout(string token) => _dal.CerrarSesion(TokenServicio.Hashear(token));

    public async Task<Cuenta> ObtenerCuenta(int usuarioId)
    {
        var cuenta = await _dal.ObtenerCuenta(usuarioId);
        cuenta.Avatares = _avatares.Catalogo;
        return cuenta;
    }

    public async Task<Cuenta> ActualizarPerfil(int usuarioId, string? nombreUsuario, string? avatarId)
    {
        var nombre = nombreUsuario?.Trim() ?? "";
        if (nombre.Length < 3 || nombre.Length > 30)
            throw new AppException(400, "El nombre de usuario debe tener entre 3 y 30 caracteres.");

        await _dal.ActualizarPerfil(usuarioId, nombre, _avatares.ObtenerUrl(avatarId));
        return await ObtenerCuenta(usuarioId);
    }

    public async Task CambiarPassword(int usuarioId, string? passwordActual, string? passwordNueva)
    {
        var actual = passwordActual ?? "";
        var nueva = passwordNueva ?? "";
        if (actual.Length == 0)
            throw new AppException(400, "Debe ingresar la contraseña actual.");
        if (!EsPasswordValida(nueva))
            throw new AppException(400, "La nueva contraseña debe tener al menos 8 caracteres, no superar 72 bytes UTF-8 e incluir una letra y un número.");

        var hash = await _dal.ObtenerPasswordHash(usuarioId);
        if (Encoding.UTF8.GetByteCount(actual) > 72 || !HashServicio.Verificar(actual, hash))
            throw new AppException(403, "La contraseña actual es incorrecta.");

        await _dal.CambiarPassword(usuarioId, hash!, HashServicio.Generar(nueva));
    }

    public async Task EliminarCuenta(int usuarioId, string? password)
    {
        var clave = password ?? "";
        if (clave.Length == 0)
            throw new AppException(400, "Debe ingresar su contraseña para eliminar la cuenta.");

        var hash = await _dal.ObtenerPasswordHash(usuarioId);
        if (!HashServicio.Verificar(clave, hash))
            throw new AppException(403, "La contraseña es incorrecta.");

        await _dal.Eliminar(usuarioId);
    }

    private async Task<SesionIniciada> IniciarSesion(Usuario usuario, string passwordHash, string? ip)
    {
        var token = TokenServicio.Generar();
        await _dal.CrearSesion(usuario.UsuarioId, TokenServicio.Hashear(token), passwordHash, ip);
        return new SesionIniciada { Token = token, Usuario = usuario };
    }

    private static bool EsCorreoValido(string correo)
    {
        if (correo.Length > 255 || !MailAddress.TryCreate(correo, out var direccion) || direccion.Address != correo)
            return false;

        var dominio = correo[(correo.IndexOf('@') + 1)..];
        return dominio.Contains('.') && !dominio.StartsWith('.') && !dominio.EndsWith('.');
    }

    private static bool EsPasswordValida(string clave) =>
        clave.Length >= 8
        && Encoding.UTF8.GetByteCount(clave) <= 72
        && clave.Any(char.IsLetter)
        && clave.Any(char.IsDigit);
}
