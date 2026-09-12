using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Text;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>Same dual-mode pattern as <see cref="RoleRepository"/> — see its doc comment.</summary>
public sealed class FolderRepository : IFolderRepository
{
    private const string SelectColumns =
        """
        SELECT f.uid, p.uid AS parent_uid, f.name, f.sort_order, f.icon_key, f.is_system, f.is_hidden,
               u.uid AS created_by, f.created_at, f.updated_at
        FROM folders f
        LEFT JOIN folders p ON p.id = f.parent_id
        LEFT JOIN app_users u ON u.id = f.created_by
        """;

    private readonly ISqliteConnectionFactory? _connectionFactory;
    private readonly IDbConnection? _boundConnection;
    private readonly IDbTransaction? _boundTransaction;

    public FolderRepository(ISqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public FolderRepository(IDbConnection connection, IDbTransaction transaction)
    {
        _boundConnection = connection;
        _boundTransaction = transaction;
    }

    public async Task<Guid> CreateAsync(NewFolder folder, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(CreateAsync));

        var uid = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var command = new CommandDefinition(
            """
            INSERT INTO folders (uid, parent_id, name, name_normalized, sort_order, is_system, is_hidden, created_by, created_at, updated_at)
            VALUES (@Uid, (SELECT id FROM folders WHERE uid = @ParentUid), @Name, @NameNormalized, @SortOrder, 0, 0,
                    (SELECT id FROM app_users WHERE uid = @CreatedBy), @Now, @Now);
            """,
            new
            {
                Uid = uid.ToString(),
                ParentUid = folder.ParentId?.ToString(),
                folder.Name,
                NameNormalized = ArabicNormalization.Normalize(folder.Name),
                folder.SortOrder,
                CreatedBy = folder.CreatedBy?.ToString(),
                Now = now,
            },
            _boundTransaction,
            cancellationToken: cancellationToken);

        await _boundConnection!.ExecuteAsync(command);
        return uid;
    }

    public Task<Folder?> GetByUidAsync(Guid uid, CancellationToken cancellationToken) =>
        QuerySingleAsync($"{SelectColumns} WHERE f.uid = @Uid;", new { Uid = uid.ToString() }, cancellationToken);

    public async Task<IReadOnlyList<Folder>> ListChildrenAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        var sql = parentId is null
            ? $"{SelectColumns} WHERE f.parent_id IS NULL ORDER BY f.sort_order, f.id;"
            : $"{SelectColumns} WHERE p.uid = @ParentUid ORDER BY f.sort_order, f.id;";
        var parameters = new { ParentUid = parentId?.ToString() };

        return await QueryListAsync(sql, parameters, cancellationToken);
    }

    public Task<IReadOnlyList<Folder>> ListAllAsync(CancellationToken cancellationToken) =>
        QueryListAsync($"{SelectColumns} ORDER BY f.sort_order, f.id;", null, cancellationToken);

    public Task RenameAsync(Guid folderUid, string newName, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(RenameAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE folders SET name = @Name, name_normalized = @NameNormalized, updated_at = @Now WHERE uid = @Uid;",
            new
            {
                Uid = folderUid.ToString(), Name = newName, NameNormalized = ArabicNormalization.Normalize(newName),
                Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task MoveAsync(Guid folderUid, Guid? newParentId, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(MoveAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE folders
            SET parent_id = (SELECT id FROM folders WHERE uid = @NewParentUid), updated_at = @Now
            WHERE uid = @Uid;
            """,
            new { Uid = folderUid.ToString(), NewParentUid = newParentId?.ToString(), Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task DeleteAsync(Guid folderUid, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(DeleteAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "DELETE FROM folders WHERE uid = @Uid;",
            new { Uid = folderUid.ToString() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public async Task<int> CountDocumentsAsync(Guid folderUid, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM documents WHERE folder_id = (SELECT id FROM folders WHERE uid = @Uid) AND deleted_at IS NULL;";
        var parameters = new { Uid = folderUid.ToString() };

        if (_boundConnection is not null)
        {
            return await _boundConnection.ExecuteScalarAsync<int>(new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<bool> HasChildrenAsync(Guid folderUid, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM folders WHERE parent_id = (SELECT id FROM folders WHERE uid = @Uid);";
        var parameters = new { Uid = folderUid.ToString() };

        int count;
        if (_boundConnection is not null)
        {
            count = await _boundConnection.ExecuteScalarAsync<int>(new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
        }
        else
        {
            await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
            count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }

        return count > 0;
    }

    private void RequireWriteMode(string memberName)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException($"{nameof(FolderRepository)}.{memberName} must run inside IUnitOfWork.ExecuteAsync.");
        }
    }

    private async Task<Folder?> QuerySingleAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        if (_boundConnection is not null)
        {
            var row = await _boundConnection.QuerySingleOrDefaultAsync<FolderRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return row is null ? null : Map(row);
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRow = await connection.QuerySingleOrDefaultAsync<FolderRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRow is null ? null : Map(readRow);
    }

    private async Task<IReadOnlyList<Folder>> QueryListAsync(string sql, object? parameters, CancellationToken cancellationToken)
    {
        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<FolderRow>(new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<FolderRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    private static Folder Map(FolderRow row) => new(
        Guid.Parse(row.Uid), row.ParentUid is null ? null : Guid.Parse(row.ParentUid), row.Name, row.SortOrder,
        row.IconKey, row.IsSystem == 1, row.IsHidden == 1, row.CreatedBy is null ? null : Guid.Parse(row.CreatedBy),
        DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAt), DateTimeOffset.FromUnixTimeMilliseconds(row.UpdatedAt));

    private sealed class FolderRow
    {
        public string Uid { get; set; } = "";
        public string? ParentUid { get; set; }
        public string Name { get; set; } = "";
        public int SortOrder { get; set; }
        public string? IconKey { get; set; }
        public long IsSystem { get; set; }
        public long IsHidden { get; set; }
        public string? CreatedBy { get; set; }
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
    }
}
