using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Core.Text;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>Same dual-mode pattern as <see cref="RoleRepository"/> — see its doc comment.</summary>
public sealed class DocumentRepository : IDocumentRepository
{
    private const string SelectColumns =
        """
        SELECT d.uid, d.archive_number, d.title, f.uid AS folder_uid, v.uid AS current_version_uid,
               d.status, d.source_type, d.storage_mode, d.document_date, d.scan_date, d.archive_date,
               cu.uid AS created_by, d.created_at, uu.uid AS updated_by, d.updated_at, d.deleted_at
        FROM documents d
        LEFT JOIN folders f ON f.id = d.folder_id
        LEFT JOIN document_versions v ON v.id = d.current_version_id
        LEFT JOIN app_users cu ON cu.id = d.created_by
        LEFT JOIN app_users uu ON uu.id = d.updated_by
        """;

    private readonly ISqliteConnectionFactory? _connectionFactory;
    private readonly IDbConnection? _boundConnection;
    private readonly IDbTransaction? _boundTransaction;

    public DocumentRepository(ISqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public DocumentRepository(IDbConnection connection, IDbTransaction transaction)
    {
        _boundConnection = connection;
        _boundTransaction = transaction;
    }

    public async Task CreateAsync(Guid uid, NewDocument document, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(CreateAsync));

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var command = new CommandDefinition(
            """
            INSERT INTO documents
                (uid, archive_number, title, title_normalized, folder_id, status, source_type, storage_mode,
                 document_date, scan_date, archive_date, created_by, created_at, updated_by, updated_at)
            VALUES
                (@Uid, @ArchiveNumber, @Title, @TitleNormalized,
                 (SELECT id FROM folders WHERE uid = @FolderUid),
                 @Status, @SourceType, @StorageMode, @DocumentDate, @ScanDate, @ArchiveDate,
                 (SELECT id FROM app_users WHERE uid = @CreatedBy), @Now,
                 (SELECT id FROM app_users WHERE uid = @CreatedBy), @Now);
            """,
            new
            {
                Uid = uid.ToString(),
                document.ArchiveNumber,
                document.Title,
                TitleNormalized = ArabicNormalization.Normalize(document.Title),
                FolderUid = document.FolderId?.ToString(),
                Status = ToDbString(DocumentStatus.Processing),
                SourceType = ToDbString(document.SourceType),
                StorageMode = ToDbString(document.StorageMode),
                DocumentDate = document.DocumentDate?.ToString("yyyy-MM-dd"),
                ScanDate = document.ScanDate?.ToString("yyyy-MM-dd"),
                ArchiveDate = document.ArchiveDate.ToString("yyyy-MM-dd"),
                CreatedBy = document.CreatedBy?.ToString(),
                Now = now,
            },
            _boundTransaction,
            cancellationToken: cancellationToken);

        await _boundConnection!.ExecuteAsync(command);
    }

    public Task<Document?> GetByUidAsync(Guid uid, CancellationToken cancellationToken) =>
        QuerySingleAsync($"{SelectColumns} WHERE d.uid = @Uid;", new { Uid = uid.ToString() }, cancellationToken);

    public Task<Document?> GetByArchiveNumberAsync(string archiveNumber, CancellationToken cancellationToken) =>
        QuerySingleAsync($"{SelectColumns} WHERE d.archive_number = @ArchiveNumber;", new { ArchiveNumber = archiveNumber }, cancellationToken);

    public async Task<IReadOnlyList<Document>> ListAsync(CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} ORDER BY d.id;";

        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<DocumentRow>(
                new CommandDefinition(sql, transaction: _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<DocumentRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<Document>> ListByFolderAsync(Guid? folderId, CancellationToken cancellationToken)
    {
        var sql = folderId is null
            ? $"{SelectColumns} WHERE d.folder_id IS NULL AND d.deleted_at IS NULL ORDER BY d.id;"
            : $"{SelectColumns} WHERE f.uid = @FolderUid AND d.deleted_at IS NULL ORDER BY d.id;";
        var parameters = new { FolderUid = folderId?.ToString() };

        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<DocumentRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<DocumentRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<Document>> ListUpdatedSinceAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} WHERE d.updated_at > @Since ORDER BY d.updated_at;";
        var parameters = new { Since = since.ToUnixTimeMilliseconds() };

        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<DocumentRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<DocumentRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    public Task SetCurrentVersionAsync(Guid documentUid, Guid versionUid, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(SetCurrentVersionAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE documents
            SET current_version_id = (SELECT id FROM document_versions WHERE uid = @VersionUid),
                status = @Status, updated_at = @Now
            WHERE uid = @DocumentUid;
            """,
            new
            {
                DocumentUid = documentUid.ToString(), VersionUid = versionUid.ToString(),
                Status = ToDbString(DocumentStatus.Active), Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task MoveToFolderAsync(Guid documentUid, Guid? folderUid, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(MoveToFolderAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE documents
            SET folder_id = (SELECT id FROM folders WHERE uid = @FolderUid), updated_at = @Now
            WHERE uid = @DocumentUid;
            """,
            new { DocumentUid = documentUid.ToString(), FolderUid = folderUid?.ToString(), Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task SetStatusAsync(Guid documentUid, DocumentStatus status, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(SetStatusAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE documents SET status = @Status, updated_at = @Now WHERE uid = @DocumentUid;",
            new { DocumentUid = documentUid.ToString(), Status = ToDbString(status), Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task SoftDeleteAsync(Guid documentUid, DateTimeOffset deletedAt, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(SoftDeleteAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE documents SET status = @Status, deleted_at = @DeletedAt, updated_at = @DeletedAt WHERE uid = @DocumentUid;",
            new { DocumentUid = documentUid.ToString(), Status = ToDbString(DocumentStatus.Trashed), DeletedAt = deletedAt.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task RestoreAsync(Guid documentUid, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(RestoreAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE documents SET status = @Status, deleted_at = NULL, updated_at = @Now WHERE uid = @DocumentUid;",
            new { DocumentUid = documentUid.ToString(), Status = ToDbString(DocumentStatus.Active), Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task PermanentDeleteAsync(Guid documentUid, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(PermanentDeleteAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "DELETE FROM documents WHERE uid = @DocumentUid;",
            new { DocumentUid = documentUid.ToString() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    private void RequireWriteMode(string memberName)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException($"{nameof(DocumentRepository)}.{memberName} must run inside IUnitOfWork.ExecuteAsync.");
        }
    }

    private async Task<Document?> QuerySingleAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        if (_boundConnection is not null)
        {
            var row = await _boundConnection.QuerySingleOrDefaultAsync<DocumentRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return row is null ? null : Map(row);
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRow = await connection.QuerySingleOrDefaultAsync<DocumentRow>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRow is null ? null : Map(readRow);
    }

    private static string ToDbString<TEnum>(TEnum value) where TEnum : struct, Enum =>
        string.Concat(value.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + c : c.ToString())).ToLowerInvariant();

    private static Document Map(DocumentRow row) => new(
        Guid.Parse(row.Uid), row.ArchiveNumber, row.Title,
        row.FolderUid is null ? null : Guid.Parse(row.FolderUid),
        row.CurrentVersionUid is null ? null : Guid.Parse(row.CurrentVersionUid),
        ParseStatus(row.Status), ParseSourceType(row.SourceType), ParseStorageMode(row.StorageMode),
        row.DocumentDate is null ? null : DateOnly.Parse(row.DocumentDate),
        row.ScanDate is null ? null : DateOnly.Parse(row.ScanDate),
        DateOnly.Parse(row.ArchiveDate),
        row.CreatedBy is null ? null : Guid.Parse(row.CreatedBy),
        DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAt),
        row.UpdatedBy is null ? null : Guid.Parse(row.UpdatedBy),
        DateTimeOffset.FromUnixTimeMilliseconds(row.UpdatedAt),
        row.DeletedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.DeletedAt.Value) : null);

    private static DocumentStatus ParseStatus(string value) => value switch
    {
        "active" => DocumentStatus.Active,
        "processing" => DocumentStatus.Processing,
        "needs_review" => DocumentStatus.NeedsReview,
        "needs_ocr" => DocumentStatus.NeedsOcr,
        "index_failed" => DocumentStatus.IndexFailed,
        "missing" => DocumentStatus.Missing,
        "trashed" => DocumentStatus.Trashed,
        "quarantined" => DocumentStatus.Quarantined,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown document status."),
    };

    private static DocumentSourceType ParseSourceType(string value) => value switch
    {
        "scan" => DocumentSourceType.Scan,
        "import" => DocumentSourceType.Import,
        "watch_folder" => DocumentSourceType.WatchFolder,
        "manual" => DocumentSourceType.Manual,
        "outlook" => DocumentSourceType.Outlook,
        "plugin" => DocumentSourceType.Plugin,
        "migration" => DocumentSourceType.Migration,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown source type."),
    };

    private static DocumentStorageMode ParseStorageMode(string value) => value switch
    {
        "managed" => DocumentStorageMode.Managed,
        "indexed_in_place" => DocumentStorageMode.IndexedInPlace,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown storage mode."),
    };

    private sealed class DocumentRow
    {
        public string Uid { get; set; } = "";
        public string ArchiveNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public string? FolderUid { get; set; }
        public string? CurrentVersionUid { get; set; }
        public string Status { get; set; } = "";
        public string SourceType { get; set; } = "";
        public string StorageMode { get; set; } = "";
        public string? DocumentDate { get; set; }
        public string? ScanDate { get; set; }
        public string ArchiveDate { get; set; } = "";
        public string? CreatedBy { get; set; }
        public long CreatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public long UpdatedAt { get; set; }
        public long? DeletedAt { get; set; }
    }
}
