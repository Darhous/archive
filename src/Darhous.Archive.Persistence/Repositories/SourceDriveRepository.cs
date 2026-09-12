using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Repositories;

public sealed class SourceDriveRepository : ISourceDriveRepository
{
    private const string SelectColumns =
        """
        SELECT uid, drive_root, volume_label, drive_type, is_enabled, auto_discover, last_seen_at, last_discovery_at
        FROM source_drives
        """;

    private readonly ISqliteConnectionFactory? _connectionFactory;
    private readonly IDbConnection? _boundConnection;
    private readonly IDbTransaction? _boundTransaction;

    public SourceDriveRepository(ISqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public SourceDriveRepository(IDbConnection connection, IDbTransaction transaction)
    {
        _boundConnection = connection;
        _boundTransaction = transaction;
    }

    public async Task UpsertSeenAsync(NewSourceDrive drive, DateTimeOffset seenAt, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(UpsertSeenAsync));

        var now = seenAt.ToUnixTimeMilliseconds();

        await _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO source_drives (uid, drive_root, volume_label, drive_type, is_enabled, auto_discover, last_seen_at, created_at, updated_at)
            VALUES (@Uid, @DriveRoot, @VolumeLabel, @DriveType, 1, @AutoDiscover, @Now, @Now, @Now)
            ON CONFLICT(drive_root) DO UPDATE SET
                volume_label = excluded.volume_label,
                drive_type = excluded.drive_type,
                last_seen_at = excluded.last_seen_at,
                updated_at = excluded.updated_at;
            """,
            new
            {
                Uid = Guid.CreateVersion7().ToString(), drive.DriveRoot, drive.VolumeLabel, drive.DriveType,
                AutoDiscover = drive.AutoDiscover ? 1 : 0, Now = now,
            },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task<IReadOnlyList<SourceDrive>> ListEnabledForAutoDiscoverAsync(CancellationToken cancellationToken) =>
        QueryListAsync($"{SelectColumns} WHERE is_enabled = 1 AND auto_discover = 1;", new { }, cancellationToken);

    public Task<IReadOnlyList<SourceDrive>> ListAllAsync(CancellationToken cancellationToken) =>
        QueryListAsync($"{SelectColumns} ORDER BY drive_root;", new { }, cancellationToken);

    public Task SetLastDiscoveryAtAsync(Guid uid, DateTimeOffset at, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(SetLastDiscoveryAtAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE source_drives SET last_discovery_at = @At WHERE uid = @Uid;",
            new { Uid = uid.ToString(), At = at.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    private void RequireWriteMode(string memberName)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException($"{nameof(SourceDriveRepository)}.{memberName} must run inside IUnitOfWork.ExecuteAsync.");
        }
    }

    private async Task<IReadOnlyList<SourceDrive>> QueryListAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<DriveRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<DriveRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    private static SourceDrive Map(DriveRow row) => new(
        Guid.Parse(row.Uid), row.DriveRoot, row.VolumeLabel, row.DriveType, row.IsEnabled != 0, row.AutoDiscover != 0,
        row.LastSeenAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.LastSeenAt.Value) : null,
        row.LastDiscoveryAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.LastDiscoveryAt.Value) : null);

    private sealed class DriveRow
    {
        public string Uid { get; set; } = "";
        public string DriveRoot { get; set; } = "";
        public string? VolumeLabel { get; set; }
        public string DriveType { get; set; } = "";
        public long IsEnabled { get; set; }
        public long AutoDiscover { get; set; }
        public long? LastSeenAt { get; set; }
        public long? LastDiscoveryAt { get; set; }
    }
}
