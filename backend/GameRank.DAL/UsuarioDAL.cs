using GameRank.BE;
using Microsoft.Data.SqlClient;

namespace GameRank.DAL;

public class UsuarioDAL
{
    private readonly AccesoDatos _datos;

    public UsuarioDAL(AccesoDatos datos)
    {
        _datos = datos;
    }

    public Task<Usuario> Registrar(string email, string nombreUsuario, string passwordHash, string versionPolitica)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_RegistrarUsuario");
            comando.Parameters.AddWithValue("@Email", email);
            comando.Parameters.AddWithValue("@NombreUsuario", nombreUsuario);
            comando.Parameters.AddWithValue("@PasswordHash", passwordHash);
            comando.Parameters.AddWithValue("@AceptaPrivacidad", true);
            comando.Parameters.AddWithValue("@VersionPolitica", versionPolitica);

            await using var lector = await comando.ExecuteReaderAsync();
            await lector.ReadAsync();
            return LeerUsuario(lector);
        });
    }

    public Task<Credenciales?> ObtenerCredenciales(string email)
    {
        return _datos.Ejecutar<Credenciales?>(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ObtenerCredenciales");
            comando.Parameters.AddWithValue("@Email", email);

            await using var lector = await comando.ExecuteReaderAsync();
            if (!await lector.ReadAsync())
                return null;

            return new Credenciales
            {
                Usuario = LeerUsuario(lector),
                PasswordHash = lector.GetString(lector.GetOrdinal("PasswordHash")),
                Bloqueado = lector.GetBoolean(lector.GetOrdinal("Bloqueado"))
            };
        });
    }

    public Task CrearSesion(int usuarioId, string tokenHash, string? ip)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_CrearSesion");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@TokenHash", tokenHash);
            comando.Parameters.AddWithValue("@IpOrigen", (object?)ip ?? DBNull.Value);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    public Task<Usuario?> ValidarSesion(string tokenHash)
    {
        return _datos.Ejecutar<Usuario?>(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ValidarSesion");
            comando.Parameters.AddWithValue("@TokenHash", tokenHash);

            await using var lector = await comando.ExecuteReaderAsync();
            return await lector.ReadAsync() ? LeerUsuario(lector) : null;
        });
    }

    public Task CerrarSesion(string tokenHash)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_CerrarSesion");
            comando.Parameters.AddWithValue("@TokenHash", tokenHash);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    private static Usuario LeerUsuario(SqlDataReader lector) => new()
    {
        UsuarioId = lector.GetInt32(lector.GetOrdinal("UsuarioId")),
        NombreUsuario = lector.GetString(lector.GetOrdinal("NombreUsuario")),
        Rol = Roles.Nombre(lector.GetByte(lector.GetOrdinal("RolId")))
    };
}
