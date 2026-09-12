namespace Darhous.Archive.Application.Persistence;

public interface IPluginRegistryRepository
{
    Task CreateAsync(NewPluginRecord plugin, CancellationToken cancellationToken);

    Task<PluginRecord?> GetByPluginIdAsync(string pluginId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PluginRecord>> ListAllAsync(CancellationToken cancellationToken);

    Task SetStatusAsync(string pluginId, string status, CancellationToken cancellationToken);

    Task SetActiveVersionAsync(string pluginId, string version, CancellationToken cancellationToken);

    Task IncrementCrashCountAsync(string pluginId, CancellationToken cancellationToken);

    Task ResetCrashCountAsync(string pluginId, CancellationToken cancellationToken);

    Task SetLastHealthAtAsync(string pluginId, DateTimeOffset at, CancellationToken cancellationToken);

    Task RemoveAsync(string pluginId, CancellationToken cancellationToken);

    Task SetPermissionAsync(string pluginId, string permission, bool granted, Guid? grantedBy, DateTimeOffset? grantedAt, CancellationToken cancellationToken);

    Task<IReadOnlyList<PluginPermissionRecord>> ListPermissionsAsync(string pluginId, CancellationToken cancellationToken);
}
