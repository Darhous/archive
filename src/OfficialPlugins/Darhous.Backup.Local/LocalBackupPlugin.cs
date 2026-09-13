using Darhous.Archive.PluginSdk;
using Darhous.Archive.PluginSdk.Configuration;
using Darhous.Archive.PluginSdk.Runtime;

namespace Darhous.Backup.Local;

/// <summary>
/// Official local-filesystem provider. Local folders, fixed drives, removable drives, and
/// mounted USB media use the same filesystem contract; destination validation happens at the
/// start of every operation because removable-media availability can change at any moment.
/// </summary>
public sealed class LocalBackupPlugin : IArchivePlugin
{
    public PluginIdentity Identity { get; } =
        new("Darhous.Backup.Local", new Version(1, 0, 0), "Darhous", "Local Backup");

    public ValueTask ConfigureAsync(IPluginConfigurationContext context, CancellationToken cancellationToken)
    {
        context.Services.AddSingleton<ILocalBackupProviderCapabilities>(new LocalBackupProviderCapabilities());
        return ValueTask.CompletedTask;
    }

    public ValueTask StartAsync(IPluginRuntimeContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask StopAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

public interface ILocalBackupProviderCapabilities
{
    bool SupportsLocalFolders { get; }
    bool SupportsFixedDrives { get; }
    bool SupportsRemovableDrives { get; }
}

internal sealed class LocalBackupProviderCapabilities : ILocalBackupProviderCapabilities
{
    public bool SupportsLocalFolders => true;
    public bool SupportsFixedDrives => true;
    public bool SupportsRemovableDrives => true;
}
