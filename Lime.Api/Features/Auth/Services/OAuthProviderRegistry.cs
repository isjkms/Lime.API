namespace Lime.Api.Features.Auth.Services;

public class OAuthProviderRegistry
{
    private readonly Dictionary<string, IOAuthProvider> _providers;

    public OAuthProviderRegistry(IEnumerable<IOAuthProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string name, out IOAuthProvider provider) =>
        _providers.TryGetValue(name, out provider!);
}
