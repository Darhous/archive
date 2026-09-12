using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Core.Text;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>Same dual-mode pattern as <see cref="RoleRepository"/> — see its doc comment.</summary>
public sealed class DocumentVersionRepository : IDocumentVersionRepository
{
    private const string SelectColumns =
        """
        SELECT v.uid, d.uid AS document_uid, v.version_no, v.original_file_name, v.stored_file_name,
               v.file_path, v.file_extension, v.mime_type, v.file_size, v.sha256, v.page_count,
               v.file_created_at, v.file_modified_at, v.imported_at, u.uid AS created_by,
               v.availability_status, v.is_searchable_pdf, v.ocr_provider, v.content_extraction_status,
               v.extracted_text, v.searchable_file_path, v.notes
        FROM document_versions v
        JOIN documents d ON d.id = v.document_id
        LEFT JOIN app_users u ON u.id = v.created_by
        """;

    private readonly ISqliteConnectionFactory? _connectionFactory;
    private readonly IDbConnection? _boundConnection;
    private readonly IDbTransaction? _boundTransaction;

    public DocumentVersionRepository(ISqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public DocumentVersionRepository(IDbConnection connection, IDbTransaction transaction)
    {
        _boundConnection = connection;
        _boundTransaction = transaction;
    }

    public async Task<Guid> CreateAsync(NewDocumentVersion version, CancellationToken cancellationToken)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException($"{nameof(DocumentVersionRepository)}.{nameof(CreateAsync)} must run inside IUnitOfWork.ExecuteAsync.");
        }

        var uid = Guid.CreateVersion7();

        var command = new CommandDefinition(
            """
            INSERT INTO document_versions
                (uid, document_id, version_no, original_file_name, stored_file_name, file_path,
                 file_path_normalized, file_extension, mime_type, file_size, sha256, page_count,
                 file_created_at, file_modified_at, imported_at, created_by, availability_status,
                 is_searchable_pdf, ocr_provider, content_extraction_status)
            VALUES
                (@Uid, (SELECT id FROM documents WHERE uid = @DocumentUid), @VersionNo, @OriginalFileName,
                 @StoredFileName, @FilePath, @FilePathNormalized, @FileExtension, @MimeType, @FileSize,
                 @Sha256, @PageCount, @FileCreatedAt, @FileModifiedAt, @ImportedAt,
                 (SELECT id FROM app_users WHERE uid = @CreatedBy), @AvailabilityStatus,
                 @IsSearchablePdf, @OcrProvider, @ContentExtractionStatus);
            """,
            new
            {
                Uid = uid.ToString(),
                DocumentUid = version.DocumentUid.ToString(),
                version.VersionNo,
                version.OriginalFileName,
                version.StoredFileName,
                version.FilePath,
                FilePathNormalized = NormalizePath(version.FilePath),
                version.FileExtension,
                version.MimeType,
                version.FileSize,
                version.Sha256,
                version.PageCount,
                FileCreatedAt = version.FileCreatedAt?.ToUnixTimeMilliseconds(),
                FileModifiedAt = version.FileModifiedAt?.ToUnixTimeMilliseconds(),
                ImportedAt = version.ImportedAt.ToUnixTimeMilliseconds(),
                CreatedBy = version.CreatedBy?.ToString(),
                AvailabilityStatus = ToSnakeCase(version.AvailabilityStatus),
                IsSearchablePdf = (bool?)null,
                version.OcrProvider,
                version.ContentExtractionStatus,
            },
            _boundTransaction,
            cancellationToken: cancellationToken);

        await _boundConnection.ExecuteAsync(command);
        return uid;
    }

    public Task<DocumentVersion?> GetByUidAsync(Guid uid, CancellationToken cancellationToken) =>
        QuerySingleAsync($"{SelectColumns} WHERE v.uid = @Uid;", new { Uid = uid.ToString() }, cancellationToken);

    public Task<DocumentVersion?> FindBySha256Async(string sha256, CancellationToken cancellationToken) =>
        QuerySingleAsync($"{SelectColumns} WHERE v.sha256 = @Sha256 LIMIT 1;", new { Sha256 = sha256 }, cancellationToken);

    public Task<DocumentVersion?> FindByFilePathAsync(string filePath, CancellationToken cancellationToken) =>
        QuerySingleAsync($"{SelectColumns} WHERE v.file_path_normalized = @Normalized LIMIT 1;", new { Normalized = NormalizePath(filePath) }, cancellationToken);

    public async Task<IReadOnlyList<DocumentVersion>> ListForDocumentAsync(Guid documentUid, CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} WHERE d.uid = @DocumentUid ORDER BY v.version_no;";
        var parameters = new { DocumentUid = documentUid.ToString() };

        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<DocumentVersionRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<DocumentVersionRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<DocumentVersion>> ListPendingExtractionAsync(int limit, CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} WHERE v.content_extraction_status = 'pending' ORDER BY v.imported_at LIMIT @Limit;";
        var parameters = new { Limit = limit };

        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<DocumentVersionRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<DocumentVersionRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<DocumentVersion>> ListNeedingOcrAsync(int limit, CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} WHERE v.content_extraction_status = 'needs_ocr' ORDER BY v.imported_at LIMIT @Limit;";
        var parameters = new { Limit = limit };

        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<DocumentVersionRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<DocumentVersionRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    public async Task UpdateExtractionResultAsync(
        Guid versionUid,
        string contentExtractionStatus,
        int? pageCount,
        bool? isSearchablePdf,
        string? extractedText,
        CancellationToken cancellationToken)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException($"{nameof(DocumentVersionRepository)}.{nameof(UpdateExtractionResultAsync)} must run inside IUnitOfWork.ExecuteAsync.");
        }

        await _boundConnection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE document_versions
            SET content_extraction_status = @Status, page_count = @PageCount,
                is_searchable_pdf = @IsSearchablePdf, extracted_text = @ExtractedText
            WHERE uid = @Uid;
            """,
            new
            {
                Uid = versionUid.ToString(), Status = contentExtractionStatus, PageCount = pageCount,
                IsSearchablePdf = isSearchablePdf, ExtractedText = extractedText,
            },
            _boundTransaction,
            cancellationToken: cancellationToken));

        await TouchParentDocumentAsync(versionUid, cancellationToken);
    }

    public async Task UpdateOcrResultAsync(
        Guid versionUid,
        string extractedText,
        string searchableFilePath,
        string ocrProvider,
        CancellationToken cancellationToken)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException($"{nameof(DocumentVersionRepository)}.{nameof(UpdateOcrResultAsync)} must run inside IUnitOfWork.ExecuteAsync.");
        }

        await _boundConnection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE document_versions
            SET content_extraction_status = 'done', is_searchable_pdf = 1,
                extracted_text = @ExtractedText, searchable_file_path = @SearchableFilePath,
                ocr_provider = @OcrProvider
            WHERE uid = @Uid;
            """,
            new
            {
                Uid = versionUid.ToString(), ExtractedText = extractedText,
                SearchableFilePath = searchableFilePath, OcrProvider = ocrProvider,
            },
            _boundTransaction,
            cancellationToken: cancellationToken));

        await TouchParentDocumentAsync(versionUid, cancellationToken);
    }

    private Task TouchParentDocumentAsync(Guid versionUid, CancellationToken cancellationToken) =>
        _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE documents
            SET updated_at = MAX(updated_at + 1, @Now)
            WHERE id = (SELECT document_id FROM document_versions WHERE uid = @Uid);
            """,
            new { Uid = versionUid.ToString(), Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));

    private async Task<DocumentVersion?> QuerySingleAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        if (_boundConnection is not null)
        {
            var row = await _boundConnection.QuerySingleOrDefaultAsync<DocumentVersionRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return row is null ? null : Map(row);
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRow = await connection.QuerySingleOrDefaultAsync<DocumentVersionRow>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRow is null ? null : Map(readRow);
    }

    private static string NormalizePath(string path) => path.Trim().ToLowerInvariant().Replace('/', '\\');

    private static string ToSnakeCase(DocumentAvailabilityStatus status) => status switch
    {
        DocumentAvailabilityStatus.Available => "available",
        DocumentAvailabilityStatus.Missing => "missing",
        DocumentAvailabilityStatus.Corrupt => "corrupt",
        DocumentAvailabilityStatus.Offline => "offline",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static DocumentAvailabilityStatus ParseAvailability(string value) => value switch
    {
        "available" => DocumentAvailabilityStatus.Available,
        "missing" => DocumentAvailabilityStatus.Missing,
        "corrupt" => DocumentAvailabilityStatus.Corrupt,
        "offline" => DocumentAvailabilityStatus.Offline,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown availability status."),
    };

    private static DocumentVersion Map(DocumentVersionRow row) => new(
        Guid.Parse(row.Uid), Guid.Parse(row.DocumentUid), row.VersionNo, row.OriginalFileName, row.StoredFileName,
        row.FilePath, row.FileExtension, row.MimeType, row.FileSize, row.Sha256, row.PageCount,
        row.FileCreatedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.FileCreatedAt.Value) : null,
        row.FileModifiedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.FileModifiedAt.Value) : null,
        DateTimeOffset.FromUnixTimeMilliseconds(row.ImportedAt),
        row.CreatedBy is null ? null : Guid.Parse(row.CreatedBy),
        ParseAvailability(row.AvailabilityStatus), row.IsSearchablePdf == 1, row.OcrProvider,
        row.ContentExtractionStatus, row.ExtractedText, row.SearchableFilePath, row.Notes);

    private sealed class DocumentVersionRow
    {
        public string Uid { get; set; } = "";
        public string DocumentUid { get; set; } = "";
        public int VersionNo { get; set; }
        public string OriginalFileName { get; set; } = "";
        public string StoredFileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string FileExtension { get; set; } = "";
        public string? MimeType { get; set; }
        public long FileSize { get; set; }
        public string Sha256 { get; set; } = "";
        public int? PageCount { get; set; }
        public long? FileCreatedAt { get; set; }
        public long? FileModifiedAt { get; set; }
        public long ImportedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string AvailabilityStatus { get; set; } = "";
        public long? IsSearchablePdf { get; set; }
        public string? OcrProvider { get; set; }
        public string ContentExtractionStatus { get; set; } = "";
        public string? ExtractedText { get; set; }
        public string? SearchableFilePath { get; set; }
        public string? Notes { get; set; }
    }
}
