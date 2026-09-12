namespace Darhous.Archive.PluginSdk.Runtime;

/// <summary>Plugin SDK §28 — DPAPI-backed, namespaced to one plugin. A plugin never sees another plugin's secrets, and secrets never pass through <c>plugin_settings</c> (non-secret only).</summary>
public interface IPluginSecrets
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, string value, CancellationToken cancellationToken);

    Task RemoveAsync(string key, CancellationToken cancellationToken);
}
