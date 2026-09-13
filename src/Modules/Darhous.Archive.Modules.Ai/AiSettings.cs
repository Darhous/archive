namespace Darhous.Archive.Modules.Ai;

public sealed record AiSettings(
    bool PrivacyAcknowledged,
    string? ActiveProviderId,
    IReadOnlyDictionary<string, bool> ProviderEnabled)
{
    public static AiSettings Disabled { get; } = new(
        PrivacyAcknowledged: false,
        ActiveProviderId: null,
        ProviderEnabled: new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase));

    public bool IsProviderEnabled(string providerId) =>
        ProviderEnabled.TryGetValue(providerId, out var enabled) && enabled;
}

public interface IAiSettingsService
{
    Task<AiSettings> GetAsync(CancellationToken cancellationToken);

    Task SetPrivacyAcknowledgedAsync(bool acknowledged, CancellationToken cancellationToken);

    Task SetProviderEnabledAsync(string providerId, bool enabled, CancellationToken cancellationToken);

    Task SetActiveProviderAsync(string? providerId, CancellationToken cancellationToken);
}

public sealed class AiPrivacyAcknowledgmentRequiredException()
    : InvalidOperationException("The cloud AI privacy warning must be acknowledged before a provider can be enabled.");
