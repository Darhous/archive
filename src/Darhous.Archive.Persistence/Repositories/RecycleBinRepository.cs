using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>Write-only-in-practice (see <see cref="IUnitOfWorkContext"/>) — recycle bin entries only ever change alongside a document trash/restore, always inside a transaction.</summary>
public sealed class RecycleBinRepository(IDbConnection connection, IDbTransaction transaction) : IRecycleBinRepository
{
    public Task AddAsync(RecycleBinEntry entry, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO recycle_bin_entries (document_id, original_folder_id, deleted_by, deleted_at, delete_reason, expires_at)
            VALUES (
                (SELECT id FROM documents WHERE uid = @DocumentUid),
                (SELECT id FROM folders WHERE uid = @OriginalFolderUid),
                (SELECT id FROM app_users WHERE uid = @DeletedBy),
                @DeletedAt, @DeleteReason, @ExpiresAt);
            """,
            new
            {
                DocumentUid = entry.DocumentUid.ToString(),
                OriginalFolderUid = entry.OriginalFolderId?.ToString(),
                DeletedBy = entry.DeletedBy?.ToString(),
                DeletedAt = entry.DeletedAt.ToUnixTimeMilliseconds(),
                entry.DeleteReason,
                ExpiresAt = entry.ExpiresAt?.ToUnixTimeMilliseconds(),
            },
            transaction,
            cancellationToken: cancellationToken));

    public Task RemoveAsync(Guid documentUid, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM recycle_bin_entries WHERE document_id = (SELECT id FROM documents WHERE uid = @DocumentUid);",
            new { DocumentUid = documentUid.ToString() },
            transaction,
            cancellationToken: cancellationToken));

    public async Task<RecycleBinEntry?> GetAsync(Guid documentUid, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT d.uid AS document_uid, f.uid AS original_folder_uid, u.uid AS deleted_by,
                   r.deleted_at, r.delete_reason, r.expires_at
            FROM recycle_bin_entries r
            JOIN documents d ON d.id = r.document_id
            LEFT JOIN folders f ON f.id = r.original_folder_id
            LEFT JOIN app_users u ON u.id = r.deleted_by
            WHERE d.uid = @DocumentUid;
            """;

        var row = await connection.QuerySingleOrDefaultAsync<RecycleBinRow>(
            new CommandDefinition(sql, new { DocumentUid = documentUid.ToString() }, transaction, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new RecycleBinEntry(
                Guid.Parse(row.DocumentUid),
                row.OriginalFolderUid is null ? null : Guid.Parse(row.OriginalFolderUid),
                row.DeletedBy is null ? null : Guid.Parse(row.DeletedBy),
                DateTimeOffset.FromUnixTimeMilliseconds(row.DeletedAt),
                row.DeleteReason,
                row.ExpiresAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.ExpiresAt.Value) : null);
    }

    private sealed class RecycleBinRow
    {
        public string DocumentUid { get; set; } = "";
        public string? OriginalFolderUid { get; set; }
        public string? DeletedBy { get; set; }
        public long DeletedAt { get; set; }
        public string? DeleteReason { get; set; }
        public long? ExpiresAt { get; set; }
    }
}
