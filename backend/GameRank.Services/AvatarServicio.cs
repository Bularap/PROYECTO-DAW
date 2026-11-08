namespace GameRank.Services;

public class AvatarServicio
{
    private readonly IReadOnlyDictionary<string, string> _avatares;

    public AvatarServicio(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var origen)
            || (origen.Scheme != Uri.UriSchemeHttp && origen.Scheme != Uri.UriSchemeHttps)
            || origen.UserInfo.Length != 0 || origen.Query.Length != 0 || origen.Fragment.Length != 0)
            throw new ArgumentException("El origen de los avatares no es válido.", nameof(baseUrl));

        var raiz = baseUrl.TrimEnd('/');
        _avatares = new Dictionary<string, string>
        {
            ["caballero"] = $"{raiz}/avatares/caballero.svg",
            ["mago"] = $"{raiz}/avatares/mago.svg",
            ["explorador"] = $"{raiz}/avatares/explorador.svg"
        };
    }

    public IReadOnlyDictionary<string, string> Catalogo => _avatares;

    public string? ObtenerUrl(string? avatarId)
    {
        if (avatarId == null)
            return null;
        if (!_avatares.TryGetValue(avatarId, out var url))
            throw new AppException(400, "El avatar seleccionado no está disponible.");
        return url;
    }
}
