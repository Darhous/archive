using System.Text.Json;
using Darhous.Archive.PluginSdk.Runtime;
using Darhous.Archive.Security.Secrets;

namespace Darhous.Archive.Modules.Plugins.Hosting;

/// <summary>Plugin SDK §28 — one DPAPI-encrypted JSON blob per plugin under its own storage directory, never mixed into <c>plugin_settings</c> (non-secret only).</summary>
public sealed class PluginSecrets(string secretsFilePath, ISecretProtector protector) : IPluginSecrets
{
    private readonly Lock _gate = new();

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(Load().GetValueOrDefault(key));
        }
    }

    public Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var secrets = Load();
            secrets[key] = value;
            Save(secrets);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var secrets = Load();
            secrets.Remove(key);
            Save(secrets);
        }

        return Task.CompletedTask;
    }

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(secretsFilePath))
        {
            return [];
        }

        var protectedJson = File.ReadAllText(secretsFilePath);
        var plainJson = protector.Unprotect(protectedJson);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(plainJson) ?? [];
    }

    private void Save(Dictionary<string, string> secrets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(secretsFilePath)!);
        var plainJson = JsonSerializer.Serialize(secrets);
        File.WriteAllText(secretsFilePath, protector.Protect(plainJson));
    }
}
