using System.Collections.Concurrent;

namespace Darhous.Archive.Modules.Ai;

public interface IAiProviderRegistry
{
    IReadOnlyList<string> ProviderIds { get; }

    bool Register(IAiProvider provider);

    bool TryGetProvider(string providerId, out IAiProvider? provider);
}

/// <summary>
/// Runtime registry for provider plugins. Provider ids are case-insensitive because plugin ids
/// are identifiers rather than user-facing labels; the first registration wins.
/// </summary>
public sealed class AiProviderRegistry : IAiProviderRegistry
{
    private readonly ConcurrentDictionary<string, IAiProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    public AiProviderRegistry(IEnumerable<IAiProvider>? providers = null)
    {
        foreach (var provider in providers ?? [])
        {
            Register(provider);
        }
    }

    public IReadOnlyList<string> ProviderIds => _providers.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();

    public bool Register(IAiProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider.ProviderId);
        return _providers.TryAdd(provider.ProviderId, provider);
    }

    public bool TryGetProvider(string providerId, out IAiProvider? provider)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            provider = null;
            return false;
        }

        return _providers.TryGetValue(providerId, out provider);
    }
}
