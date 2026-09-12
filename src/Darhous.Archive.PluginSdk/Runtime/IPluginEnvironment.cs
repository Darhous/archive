namespace Darhous.Archive.PluginSdk.Runtime;

/// <summary>Read-only environment facts a plugin may legitimately need — never a way to reach outside its own sandbox.</summary>
public interface IPluginEnvironment
{
    Version CoreVersion { get; }

    string InstalledVersion { get; }

    bool IsDeveloperMode { get; }

    /// <summary>
    /// The directory this plugin's own version was installed into. Prefer <see cref="IPluginStorage"/>
    /// for anything a plugin writes — this is read-only and exists mainly so a plugin can locate its
    /// own bundled resources; the entry assembly's own <c>Assembly.Location</c> is unreliable here since
    /// the host loads plugin assemblies from a byte stream (to avoid file-locking installed DLLs), which
    /// makes .NET report an empty <c>Location</c> for them.
    /// </summary>
    string InstallDirectory { get; }
}
