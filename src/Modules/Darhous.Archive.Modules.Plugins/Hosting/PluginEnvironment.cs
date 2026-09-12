using Darhous.Archive.PluginSdk.Runtime;

namespace Darhous.Archive.Modules.Plugins.Hosting;

public sealed class PluginEnvironment(Version coreVersion, string installedVersion, bool isDeveloperMode, string installDirectory) : IPluginEnvironment
{
    public Version CoreVersion { get; } = coreVersion;
    public string InstalledVersion { get; } = installedVersion;
    public bool IsDeveloperMode { get; } = isDeveloperMode;
    public string InstallDirectory { get; } = installDirectory;
}
