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

    public Task CrearSesion(int usuarioId, string tokenHash, string passwordHashEsperado, string? ip)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_CrearSesion");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@TokenHash", tokenHash);
            comando.Parameters.AddWithValue("@PasswordHashEsperado", passwordHashEsperado);
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

    public Task<string?> ObtenerPasswordHash(int usuarioId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ObtenerPasswordHash");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            return (string?)await comando.ExecuteScalarAsync();
        });
    }

    public Task Eliminar(int usuarioId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_EliminarUsuario");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    public Task<Cuenta> ObtenerCuenta(int usuarioId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ObtenerCuenta");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            await using var lector = await comando.ExecuteReaderAsync();
            if (!await lector.ReadAsync())
                throw new GameRank.Services.AppException(401, "La cuenta ya no está disponible.");
            return new Cuenta
            {
                UsuarioId = lector.GetInt32(lector.GetOrdinal("UsuarioId")),
                NombreUsuario = lector.GetString(lector.GetOrdinal("NombreUsuario")),
                Email = lector.GetString(lector.GetOrdinal("Email")),
                AvatarUrl = lector.IsDBNull(lector.GetOrdinal("AvatarUrl")) ? null : lector.GetString(lector.GetOrdinal("AvatarUrl"))
            };
        });
    }

    public Task ActualizarPerfil(int usuarioId, string nombreUsuario, string? avatarUrl)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ActualizarPerfil");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@NombreUsuario", nombreUsuario);
            comando.Parameters.AddWithValue("@AvatarUrl", (object?)avatarUrl ?? DBNull.Value);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    public Task CambiarPassword(int usuarioId, string passwordHashEsperado, string passwordHashNuevo)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_CambiarPassword");
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@PasswordHashEsperado", passwordHashEsperado);
            comando.Parameters.AddWithValue("@PasswordHashNuevo", passwordHashNuevo);
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
