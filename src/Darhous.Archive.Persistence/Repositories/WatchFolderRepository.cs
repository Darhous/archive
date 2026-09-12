using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Repositories;

public sealed class WatchFolderRepository : IWatchFolderRepository
{
    private const string SelectColumns =
        """
        SELECT w.uid, w.path, w.include_subfolders, w.import_mode, f.uid AS destination_folder_id,
               w.is_enabled, w.last_reconciled_at, u.uid AS created_by, w.created_at
        FROM watch_folders w
        LEFT JOIN folders f ON f.id = w.destination_folder_id
        LEFT JOIN app_users u ON u.id = w.created_by
        """;

    private readonly ISqliteConnectionFactory? _connectionFactory;
    private readonly IDbConnection? _boundConnection;
    private readonly IDbTransaction? _boundTransaction;

    public WatchFolderRepository(ISqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public WatchFolderRepository(IDbConnection connection, IDbTransaction transaction)
    {
        _boundConnection = connection;
        _boundTransaction = transaction;
    }

    public async Task<Guid> CreateAsync(NewWatchFolder folder, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(CreateAsync));

        var uid = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO watch_folders (uid, path, include_subfolders, import_mode, destination_folder_id, is_enabled, created_by, created_at)
            VALUES (@Uid, @Path, @IncludeSubfolders, @ImportMode,
                    (SELECT id FROM folders WHERE uid = @DestinationFolderId),
                    1, (SELECT id FROM app_users WHERE uid = @CreatedBy), @Now);
            """,
            new
            {
                Uid = uid.ToString(), folder.Path, IncludeSubfolders = folder.IncludeSubfolders ? 1 : 0, folder.ImportMode,
                DestinationFolderId = folder.DestinationFolderId?.ToString(), CreatedBy = folder.CreatedBy?.ToString(), Now = now,
            },
            _boundTransaction,
            cancellationToken: cancellationToken));

        return uid;
    }

    public Task<IReadOnlyList<WatchFolder>> ListEnabledAsync(CancellationToken cancellationToken) =>
        QueryListAsync($"{SelectColumns} WHERE w.is_enabled = 1 ORDER BY w.created_at;", new { }, cancellationToken);

    public async Task<WatchFolder?> GetByPathAsync(string path, CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} WHERE w.path = @Path;";
        var parameters = new { Path = path };

        if (_boundConnection is not null)
        {
            var row = await _boundConnection.QuerySingleOrDefaultAsync<WatchFolderRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return row is null ? null : Map(row);
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRow = await connection.QuerySingleOrDefaultAsync<WatchFolderRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRow is null ? null : Map(readRow);
    }

    public Task SetLastReconciledAsync(Guid uid, DateTimeOffset reconciledAt, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(SetLastReconciledAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE watch_folders SET last_reconciled_at = @ReconciledAt WHERE uid = @Uid;",
            new { Uid = uid.ToString(), ReconciledAt = reconciledAt.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task DisableAsync(Guid uid, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(DisableAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE watch_folders SET is_enabled = 0 WHERE uid = @Uid;",
            new { Uid = uid.ToString() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    private void RequireWriteMode(string memberName)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException($"{nameof(WatchFolderRepository)}.{memberName} must run inside IUnitOfWork.ExecuteAsync.");
        }
    }

    private async Task<IReadOnlyList<WatchFolder>> QueryListAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<WatchFolderRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<WatchFolderRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    private static WatchFolder Map(WatchFolderRow row) => new(
        Guid.Parse(row.Uid), row.Path, row.IncludeSubfolders != 0, row.ImportMode,
        row.DestinationFolderId is null ? null : Guid.Parse(row.DestinationFolderId),
        row.IsEnabled != 0,
        row.LastReconciledAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.LastReconciledAt.Value) : null,
        row.CreatedBy is null ? null : Guid.Parse(row.CreatedBy),
        DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAt));

    private sealed class WatchFolderRow
    {
        public string Uid { get; set; } = "";
        public string Path { get; set; } = "";
        public long IncludeSubfolders { get; set; }
        public string ImportMode { get; set; } = "";
        public string? DestinationFolderId { get; set; }
        public long IsEnabled { get; set; }
        public long? LastReconciledAt { get; set; }
        public string? CreatedBy { get; set; }
        public long CreatedAt { get; set; }
    }
}
