using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Darhous.Archive.Configuration;
using Darhous.Archive.Security.Secrets;

namespace Darhous.Archive.Modules.Ai;

public interface IAiSecretStore
{
    Task<string?> GetApiKeyAsync(string providerId, CancellationToken cancellationToken);

    Task SetApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken);

    Task ClearApiKeyAsync(string providerId, CancellationToken cancellationToken);
}

/// <summary>
/// Stores only DPAPI-protected values in app_settings. The provider id is hashed into the key
/// solely to keep arbitrary plugin identifiers out of the settings namespace; it is not secret.
/// </summary>
public sealed class AiSecretStore(IAppSettingsStore store, ISecretProtector protector) : IAiSecretStore
{
    public async Task<string?> GetApiKeyAsync(string providerId, CancellationToken cancellationToken)
    {
        var protectedJson = await store.GetAsync(GetStorageKey(providerId), cancellationToken);
        if (string.IsNullOrWhiteSpace(protectedJson) || protectedJson == "null")
        {
            return null;
        }

        var protectedValue = JsonSerializer.Deserialize<string>(protectedJson);
        return string.IsNullOrEmpty(protectedValue) ? null : protector.Unprotect(protectedValue);
    }

    public Task SetApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var protectedValue = protector.Protect(apiKey);
        return store.SetAsync(GetStorageKey(providerId), JsonSerializer.Serialize(protectedValue), cancellationToken);
    }

    public Task ClearApiKeyAsync(string providerId, CancellationToken cancellationToken) =>
        store.SetAsync(GetStorageKey(providerId), "null", cancellationToken);

    private static string GetStorageKey(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(providerId.ToUpperInvariant()));
        return $"ai.secret.v1.{Convert.ToHexString(digest)}";
    }
}
