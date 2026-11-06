namespace GameRank.BE;

public static class Roles
{
    public const string Jugador = "Jugador";
    public const string Administrador = "Administrador";
    public const string SuperAdministrador = "SuperAdministrador";

    public static string Nombre(byte rolId) => rolId switch
    {
        2 => Administrador,
        3 => SuperAdministrador,
        _ => Jugador
    };
}
