using GameRank.BE;
using static GameRank.DAL.LectorDatos;

namespace GameRank.DAL;

public class AdministracionDAL
{
    private readonly AccesoDatos _datos;

    public AdministracionDAL(AccesoDatos datos)
    {
        _datos = datos;
    }

    public Task<ListaPaginada<UsuarioAdmin>> ListarUsuarios(string? texto, bool soloBloqueados, int pagina, int tamanioPagina)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ListarUsuarios");
            comando.Parameters.AddWithValue("@Texto", (object?)texto ?? DBNull.Value);
            comando.Parameters.AddWithValue("@SoloBloqueados", soloBloqueados);
            comando.Parameters.AddWithValue("@Pagina", pagina);
            comando.Parameters.AddWithValue("@TamanioPagina", tamanioPagina);

            var resultado = new ListaPaginada<UsuarioAdmin> { Pagina = pagina, TamanioPagina = tamanioPagina };

            await using var lector = await comando.ExecuteReaderAsync();
            while (await lector.ReadAsync())
            {
                var rolId = lector.GetByte(lector.GetOrdinal("RolId"));

                resultado.Total = lector.GetInt32(lector.GetOrdinal("Total"));
                resultado.Items.Add(new UsuarioAdmin
                {
                    UsuarioId = lector.GetInt32(lector.GetOrdinal("UsuarioId")),
                    NombreUsuario = lector.GetString(lector.GetOrdinal("NombreUsuario")),
                    Email = lector.GetString(lector.GetOrdinal("Email")),
                    RolId = rolId,
                    Rol = Roles.Nombre(rolId),
                    Bloqueado = lector.GetBoolean(lector.GetOrdinal("Bloqueado")),
                    FechaRegistro = FechaUtc(lector, "FechaRegistro"),
                    UltimoLogin = FechaUtcONula(lector, "UltimoLogin")
                });
            }

            return resultado;
        });
    }

    public Task<List<Rol>> ListarRoles()
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ListarRoles");
            await using var lector = await comando.ExecuteReaderAsync();

            var roles = new List<Rol>();
            while (await lector.ReadAsync())
            {
                roles.Add(new Rol
                {
                    RolId = lector.GetByte(lector.GetOrdinal("RolId")),
                    Nombre = lector.GetString(lector.GetOrdinal("Nombre")),
                    Descripcion = TextoONulo(lector, "Descripcion")
                });
            }

            return roles;
        });
    }

    public Task AsignarRol(int superAdminId, int usuarioId, byte rolId)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_AsignarRol");
            comando.Parameters.AddWithValue("@SuperAdminId", superAdminId);
            comando.Parameters.AddWithValue("@UsuarioId", usuarioId);
            comando.Parameters.AddWithValue("@RolId", rolId);
            return await comando.ExecuteNonQueryAsync();
        });
    }

    public Task<ListaPaginada<RegistroAuditoria>> ListarAuditoria(int pagina, int tamanioPagina)
    {
        return _datos.Ejecutar(async conexion =>
        {
            await using var comando = AccesoDatos.Procedimiento(conexion, "dbo.sp_ListarAuditoria");
            comando.Parameters.AddWithValue("@Pagina", pagina);
            comando.Parameters.AddWithValue("@TamanioPagina", tamanioPagina);

            var resultado = new ListaPaginada<RegistroAuditoria> { Pagina = pagina, TamanioPagina = tamanioPagina };

            await using var lector = await comando.ExecuteReaderAsync();
            while (await lector.ReadAsync())
            {
                resultado.Total = lector.GetInt32(lector.GetOrdinal("Total"));
                resultado.Items.Add(new RegistroAuditoria
                {
                    AuditoriaId = lector.GetInt64(lector.GetOrdinal("AuditoriaId")),
                    UsuarioActorId = lector.GetInt32(lector.GetOrdinal("UsuarioActorId")),
                    Actor = lector.GetString(lector.GetOrdinal("Actor")),
                    Accion = lector.GetString(lector.GetOrdinal("Accion")),
                    EntidadTipo = lector.GetString(lector.GetOrdinal("EntidadTipo")),
                    EntidadId = lector.GetInt32(lector.GetOrdinal("EntidadId")),
                    Motivo = TextoONulo(lector, "Motivo"),
                    Detalle = TextoONulo(lector, "Detalle"),
                    Fecha = FechaUtc(lector, "Fecha")
                });
            }

            return resultado;
        });
    }
}
