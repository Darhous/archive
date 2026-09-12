using Dapper;
using Darhous.Archive.Core.Text;
using Darhous.Archive.Persistence.Writes;
using Darhous.Search.SqliteFts.Availability;
using Microsoft.Extensions.Logging;

namespace Darhous.Search.SqliteFts.Indexing;

/// <summary>
/// <c>documents_fts.rowid</c> is kept equal to <c>search_documents.document_id</c> (Migration
/// doc comment) so upsert/remove never need a join — <c>WHERE rowid = @DocumentId</c> is a
/// direct FTS5 shadow-table lookup.
/// </summary>
public sealed class SearchIndexWriter(
    ISqliteWriteQueue writeQueue, ISearchAvailability availability, ILogger<SearchIndexWriter> logger)
    : ISearchIndexWriter
{
    public async Task UpsertAsync(SearchDocumentSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            await writeQueue.EnqueueAsync(async (connection, transaction, ct) =>
            {
                var titleNormalized = ArabicNormalization.Normalize(snapshot.Title);
                var fileNameNormalized = ArabicNormalization.Normalize(snapshot.FileName);
                var bodyNormalized = snapshot.Body is null ? string.Empty : ArabicNormalization.Normalize(snapshot.Body);
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var archiveDate = snapshot.ArchiveDate.ToString("yyyy-MM-dd");

                var existingId = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
                    "SELECT document_id FROM search_documents WHERE document_uid = @Uid;",
                    new { Uid = snapshot.DocumentUid.ToString() }, transaction, cancellationToken: ct));

                long documentId;
                if (existingId is { } id)
                {
                    documentId = id;

                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        UPDATE search_documents
                        SET archive_number = @ArchiveNumber, title_display = @Title, folder_id = @FolderId,
                            archive_date = @ArchiveDate, file_type = @FileType, status = @Status,
                            is_searchable = 1, indexed_at = @Now, index_status = 'indexed', error_code = NULL
                        WHERE document_id = @DocumentId;
                        """,
                        new
                        {
                            snapshot.ArchiveNumber, snapshot.Title, FolderId = snapshot.FolderId?.ToString(),
                            ArchiveDate = archiveDate, snapshot.FileType, snapshot.Status, Now = now, DocumentId = documentId,
                        },
                        transaction, cancellationToken: ct));

                    await connection.ExecuteAsync(new CommandDefinition(
                        "UPDATE documents_fts SET title = @Title, file_name = @FileName, body = @Body WHERE rowid = @DocumentId;",
                        new { Title = titleNormalized, FileName = fileNameNormalized, Body = bodyNormalized, DocumentId = documentId },
                        transaction, cancellationToken: ct));
                }
                else
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO search_documents
                            (document_uid, archive_number, title_display, folder_id, archive_date, file_type,
                             status, is_searchable, indexed_at, index_status)
                        VALUES
                            (@DocumentUid, @ArchiveNumber, @Title, @FolderId, @ArchiveDate, @FileType,
                             @Status, 1, @Now, 'indexed');
                        """,
                        new
                        {
                            DocumentUid = snapshot.DocumentUid.ToString(), snapshot.ArchiveNumber, snapshot.Title,
                            FolderId = snapshot.FolderId?.ToString(), ArchiveDate = archiveDate, snapshot.FileType,
                            snapshot.Status, Now = now,
                        },
                        transaction, cancellationToken: ct));

                    documentId = await connection.QuerySingleAsync<long>(new CommandDefinition(
                        "SELECT last_insert_rowid();", transaction: transaction, cancellationToken: ct));

                    await connection.ExecuteAsync(new CommandDefinition(
                        "INSERT INTO documents_fts(rowid, document_id, title, file_name, metadata, body) VALUES (@Id, @Id, @Title, @FileName, '', @Body);",
                        new { Id = documentId, Title = titleNormalized, FileName = fileNameNormalized, Body = bodyNormalized },
                        transaction, cancellationToken: ct));
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO search_state (document_uid, source_updated_at, normalizer_version, indexed_at)
                    VALUES (@DocumentUid, @SourceUpdatedAt, @NormalizerVersion, @Now)
                    ON CONFLICT(document_uid) DO UPDATE SET
                        source_updated_at = excluded.source_updated_at,
                        normalizer_version = excluded.normalizer_version,
                        indexed_at = excluded.indexed_at;
                    """,
                    new
                    {
                        DocumentUid = snapshot.DocumentUid.ToString(),
                        SourceUpdatedAt = snapshot.SourceUpdatedAt.ToUnixTimeMilliseconds(),
                        NormalizerVersion = NormalizerVersion.Current, Now = now,
                    },
                    transaction, cancellationToken: ct));

                return true;
            }, cancellationToken);

            availability.MarkAvailable();
        }
        catch (Exception ex) when (SearchAvailability.IsCorruption(ex))
        {
            availability.MarkUnavailable(ex);
        }
        catch (Exception ex)
        {
            // Any other failure (e.g. one malformed row) must not stop indexing of the next
            // document (SAD §5.1 Core Stability Principle) — the reconciliation sweep will
            // simply retry this document next tick since its checkpoint never advanced past it.
            logger.LogError(ex, "Failed to index document {DocumentUid} for search", snapshot.DocumentUid);
        }
    }

    public async Task RemoveAsync(Guid documentUid, CancellationToken cancellationToken)
    {
        try
        {
            await writeQueue.EnqueueAsync(async (connection, transaction, ct) =>
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    DELETE FROM documents_fts
                    WHERE rowid IN (SELECT document_id FROM search_documents WHERE document_uid = @Uid);
                    """,
                    new { Uid = documentUid.ToString() }, transaction, cancellationToken: ct));

                await connection.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM search_documents WHERE document_uid = @Uid;",
                    new { Uid = documentUid.ToString() }, transaction, cancellationToken: ct));

                await connection.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM search_state WHERE document_uid = @Uid;",
                    new { Uid = documentUid.ToString() }, transaction, cancellationToken: ct));

                return true;
            }, cancellationToken);

            availability.MarkAvailable();
        }
        catch (Exception ex) when (SearchAvailability.IsCorruption(ex))
        {
            availability.MarkUnavailable(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove document {DocumentUid} from search index", documentUid);
        }
    }
}
