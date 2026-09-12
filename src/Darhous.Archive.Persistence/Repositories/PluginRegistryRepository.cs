using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>Same dual-mode pattern as <see cref="DocumentRepository"/> — keyed by <c>plugin_id</c> (a stable string, per Plugin SDK §11) rather than a Guid uid.</summary>
public sealed class PluginRegistryRepository : IPluginRegistryRepository
{
    private const string SelectColumns =
        """
        SELECT plugin_id, installed_version, active_version, publisher, trust_level, status,
               update_channel, package_hash, crash_count, installed_at, updated_at, last_health_at
        FROM plugins
        """;

    private readonly ISqliteConnectionFactory? _connectionFactory;
    private readonly IDbConnection? _boundConnection;
    private readonly IDbTransaction? _boundTransaction;

    public PluginRegistryRepository(ISqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public PluginRegistryRepository(IDbConnection connection, IDbTransaction transaction)
    {
        _boundConnection = connection;
        _boundTransaction = transaction;
    }

    public Task CreateAsync(NewPluginRecord plugin, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(CreateAsync));

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO plugins (plugin_id, installed_version, active_version, publisher, trust_level, status, update_channel, package_hash, crash_count, installed_at, updated_at)
            VALUES (@PluginId, @InstalledVersion, @ActiveVersion, @Publisher, @TrustLevel, @Status, @UpdateChannel, @PackageHash, 0, @Now, @Now);
            """,
            new
            {
                plugin.PluginId, plugin.InstalledVersion, plugin.ActiveVersion, plugin.Publisher,
                plugin.TrustLevel, plugin.Status, plugin.UpdateChannel, plugin.PackageHash, Now = now,
            },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public async Task<PluginRecord?> GetByPluginIdAsync(string pluginId, CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} WHERE plugin_id = @PluginId;";
        var parameters = new { PluginId = pluginId };

        if (_boundConnection is not null)
        {
            var row = await _boundConnection.QuerySingleOrDefaultAsync<PluginRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return row is null ? null : Map(row);
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRow = await connection.QuerySingleOrDefaultAsync<PluginRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRow is null ? null : Map(readRow);
    }

    public async Task<IReadOnlyList<PluginRecord>> ListAllAsync(CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} ORDER BY plugin_id;";

        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<PluginRow>(
                new CommandDefinition(sql, transaction: _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<PluginRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    public Task SetStatusAsync(string pluginId, string status, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(SetStatusAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE plugins SET status = @Status, updated_at = @Now WHERE plugin_id = @PluginId;",
            new { PluginId = pluginId, Status = status, Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task SetActiveVersionAsync(string pluginId, string version, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(SetActiveVersionAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE plugins SET active_version = @Version, installed_version = @Version, updated_at = @Now WHERE plugin_id = @PluginId;",
            new { PluginId = pluginId, Version = version, Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task IncrementCrashCountAsync(string pluginId, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(IncrementCrashCountAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE plugins SET crash_count = crash_count + 1, updated_at = @Now WHERE plugin_id = @PluginId;",
            new { PluginId = pluginId, Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task ResetCrashCountAsync(string pluginId, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(ResetCrashCountAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE plugins SET crash_count = 0, updated_at = @Now WHERE plugin_id = @PluginId;",
            new { PluginId = pluginId, Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task SetLastHealthAtAsync(string pluginId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(SetLastHealthAtAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE plugins SET last_health_at = @At WHERE plugin_id = @PluginId;",
            new { PluginId = pluginId, At = at.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task RemoveAsync(string pluginId, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(RemoveAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "DELETE FROM plugin_permissions WHERE plugin_id = @PluginId; DELETE FROM plugin_settings WHERE plugin_id = @PluginId; DELETE FROM plugins WHERE plugin_id = @PluginId;",
            new { PluginId = pluginId },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task SetPermissionAsync(string pluginId, string permission, bool granted, Guid? grantedBy, DateTimeOffset? grantedAt, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(SetPermissionAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO plugin_permissions (plugin_id, permission, granted, granted_by, granted_at)
            VALUES (@PluginId, @Permission, @Granted, (SELECT id FROM app_users WHERE uid = @GrantedBy), @GrantedAt)
            ON CONFLICT(plugin_id, permission) DO UPDATE SET
                granted = excluded.granted,
                granted_by = excluded.granted_by,
                granted_at = excluded.granted_at;
            """,
            new
            {
                PluginId = pluginId, Permission = permission, Granted = granted ? 1 : 0,
                GrantedBy = grantedBy?.ToString(), GrantedAt = grantedAt?.ToUnixTimeMilliseconds(),
            },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PluginPermissionRecord>> ListPermissionsAsync(string pluginId, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT p.plugin_id, p.permission, p.granted, u.uid AS granted_by, p.granted_at
            FROM plugin_permissions p
            LEFT JOIN app_users u ON u.id = p.granted_by
            WHERE p.plugin_id = @PluginId;
            """;
        var parameters = new { PluginId = pluginId };

        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<PermissionRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(MapPermission).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<PermissionRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRows.Select(MapPermission).ToList();
    }

    private void RequireWriteMode(string memberName)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException($"{nameof(PluginRegistryRepository)}.{memberName} must run inside IUnitOfWork.ExecuteAsync.");
        }
    }

    private static PluginRecord Map(PluginRow row) => new(
        row.PluginId, row.InstalledVersion, row.ActiveVersion, row.Publisher, row.TrustLevel, row.Status,
        row.UpdateChannel, row.PackageHash, row.CrashCount, DateTimeOffset.FromUnixTimeMilliseconds(row.InstalledAt),
        DateTimeOffset.FromUnixTimeMilliseconds(row.UpdatedAt),
        row.LastHealthAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.LastHealthAt.Value) : null);

    private static PluginPermissionRecord MapPermission(PermissionRow row) => new(
        row.PluginId, row.Permission, row.Granted != 0, row.GrantedBy is null ? null : Guid.Parse(row.GrantedBy),
        row.GrantedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.GrantedAt.Value) : null);

    private sealed class PluginRow
    {
        public string PluginId { get; set; } = "";
        public string InstalledVersion { get; set; } = "";
        public string ActiveVersion { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string TrustLevel { get; set; } = "";
        public string Status { get; set; } = "";
        public string UpdateChannel { get; set; } = "";
        public string PackageHash { get; set; } = "";
        public int CrashCount { get; set; }
        public long InstalledAt { get; set; }
        public long UpdatedAt { get; set; }
        public long? LastHealthAt { get; set; }
    }

    private sealed class PermissionRow
    {
        public string PluginId { get; set; } = "";
        public string Permission { get; set; } = "";
        public long Granted { get; set; }
        public string? GrantedBy { get; set; }
        public long? GrantedAt { get; set; }
    }
}
