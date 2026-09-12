using Darhous.Archive.Configuration;

namespace Darhous.Archive.Modules.Plugins;

/// <summary>Defaults to <see cref="AppPaths"/>; tests override both to an isolated temp directory (same pattern as <c>DocumentStorageOptions</c>) so nothing here ever touches the real %ProgramData%.</summary>
public sealed class PluginPlatformOptions
{
    public string PluginsRoot { get; init; } = AppPaths.Plugins;

    public string PluginDataRoot { get; init; } = AppPaths.PluginData;

    public string TrustStoreDirectory { get; init; } = Path.Combine(AppPaths.ProgramDataRoot, "PluginTrust");
}
