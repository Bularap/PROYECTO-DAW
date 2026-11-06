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

    public AuthBLL(UsuarioDAL dal)
    {
        _dal = dal;
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

        var usuario = await _dal.Registrar(correo, nombre, HashServicio.Generar(clave), VersionPolitica);
        return await IniciarSesion(usuario, ip);
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

        return await IniciarSesion(credenciales.Usuario, ip);
    }

    public Task<Usuario?> ValidarToken(string token) => _dal.ValidarSesion(TokenServicio.Hashear(token));

    public Task Logout(string token) => _dal.CerrarSesion(TokenServicio.Hashear(token));

    private async Task<SesionIniciada> IniciarSesion(Usuario usuario, string? ip)
    {
        var token = TokenServicio.Generar();
        await _dal.CrearSesion(usuario.UsuarioId, TokenServicio.Hashear(token), ip);
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
