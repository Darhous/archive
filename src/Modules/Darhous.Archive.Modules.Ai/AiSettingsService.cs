using System.Text.Json;
using Darhous.Archive.Configuration;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Ai;

/// <summary>
/// Persists the complete Phase-20 AI configuration in the existing app_settings key/value store.
/// All defaults fail closed: absent or malformed settings mean no acknowledged warning, no active
/// provider, and no enabled providers.
/// </summary>
public sealed class AiSettingsService(
    IAppSettingsStore store,
    IAiProviderRegistry registry,
    ILogger<AiSettingsService> logger) : IAiSettingsService
{
    internal const string SettingsKey = "ai.settings.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<AiSettings> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetPrivacyAcknowledgedAsync(bool acknowledged, CancellationToken cancellationToken)
    {
        await MutateAsync(
            current => acknowledged
                ? current with { PrivacyAcknowledged = true }
                : AiSettings.Disabled,
            cancellationToken);
    }

    public async Task SetProviderEnabledAsync(
        string providerId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        await MutateAsync(
            current =>
            {
                if (enabled && !current.PrivacyAcknowledged)
                {
                    throw new AiPrivacyAcknowledgmentRequiredException();
                }

                if (enabled && !registry.TryGetProvider(providerId, out _))
                {
                    throw new KeyNotFoundException($"AI provider '{providerId}' is not registered.");
                }

                var providerStates = new Dictionary<string, bool>(
                    current.ProviderEnabled,
                    StringComparer.OrdinalIgnoreCase)
                {
                    [providerId] = enabled,
                };

                return current with
                {
                    ActiveProviderId = !enabled && string.Equals(
                        current.ActiveProviderId,
                        providerId,
                        StringComparison.OrdinalIgnoreCase)
                            ? null
                            : current.ActiveProviderId,
                    ProviderEnabled = providerStates,
                };
            },
            cancellationToken);
    }

    public async Task SetActiveProviderAsync(string? providerId, CancellationToken cancellationToken)
    {
        await MutateAsync(
            current =>
            {
                if (string.IsNullOrWhiteSpace(providerId))
                {
                    return current with { ActiveProviderId = null };
                }

                if (!current.PrivacyAcknowledged)
                {
                    throw new AiPrivacyAcknowledgmentRequiredException();
                }

                if (!registry.TryGetProvider(providerId, out _))
                {
                    throw new KeyNotFoundException($"AI provider '{providerId}' is not registered.");
                }

                if (!current.IsProviderEnabled(providerId))
                {
                    throw new InvalidOperationException($"AI provider '{providerId}' must be enabled before it can be active.");
                }

                return current with { ActiveProviderId = providerId };
            },
            cancellationToken);
    }

    private async Task MutateAsync(Func<AiSettings, AiSettings> mutation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var updated = mutation(await LoadAsync(cancellationToken));
            var persisted = new PersistedAiSettings(
                updated.PrivacyAcknowledged,
                updated.ActiveProviderId,
                new Dictionary<string, bool>(updated.ProviderEnabled, StringComparer.OrdinalIgnoreCase));
            await store.SetAsync(SettingsKey, JsonSerializer.Serialize(persisted, JsonOptions), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AiSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var json = await store.GetAsync(SettingsKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return AiSettings.Disabled;
        }

        try
        {
            var persisted = JsonSerializer.Deserialize<PersistedAiSettings>(json, JsonOptions);
            if (persisted is null)
            {
                return AiSettings.Disabled;
            }

            return new AiSettings(
                persisted.PrivacyAcknowledged,
                persisted.PrivacyAcknowledged ? persisted.ActiveProviderId : null,
                persisted.PrivacyAcknowledged
                    ? new Dictionary<string, bool>(persisted.ProviderEnabled ?? [], StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase));
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "AI settings are malformed; cloud AI remains disabled");
            return AiSettings.Disabled;
        }
    }

    private sealed record PersistedAiSettings(
        bool PrivacyAcknowledged,
        string? ActiveProviderId,
        Dictionary<string, bool>? ProviderEnabled);
}
