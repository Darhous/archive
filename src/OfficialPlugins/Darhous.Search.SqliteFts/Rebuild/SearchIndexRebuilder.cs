using Dapper;
using Darhous.Archive.Persistence.Writes;
using Darhous.Search.SqliteFts.Availability;
using Darhous.Search.SqliteFts.Indexing;
using Microsoft.Extensions.Logging;

namespace Darhous.Search.SqliteFts.Rebuild;

/// <summary>
/// Deliberately NOT a physical "build search.db.tmp then swap the file" rebuild (the
/// approach originally reviewed with Codex/AgentFlow) — that pattern exists to let queries
/// keep running against the old file while a new one builds elsewhere, which matters when
/// rebuild is slow/expensive or many processes hold open handles. Here it's simpler:
/// search.db has exactly one writer connection (<see cref="ISqliteWriteQueue"/>, held for
/// the app's lifetime) and only ever-short-lived reader connections, so re-creating the
/// schema in place through that same writer connection is transactionally safe with no
/// file-handle juggling, no WAL/journal residue, and no Windows file-replace-while-open
/// failure mode. The cost is a brief window where searches return nothing while the schema
/// is being recreated — acceptable for a single-user desktop app triggering this rarely and
/// explicitly (DB Spec §97: browsing must keep working, which this doesn't touch at all).
/// </summary>
public sealed class SearchIndexRebuilder(
    ISqliteWriteQueue writeQueue, SearchReconciliationService reconciliationService,
    ISearchAvailability availability, ILogger<SearchIndexRebuilder> logger)
    : ISearchIndexRebuilder
{
    public async Task RebuildAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Search index rebuild starting");

        await writeQueue.EnqueueAsync(async (connection, transaction, ct) =>
        {
            // Same DDL as M202609120001_InitialSearchSchema — corruption can be structural,
            // not just bad rows, so recreating the tables from scratch (not just DELETE FROM)
            // is the actual recovery action, matching DB Spec §157.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                DROP TABLE IF EXISTS search_state;
                DROP TABLE IF EXISTS search_document_tags;
                DROP TABLE IF EXISTS search_documents;
                DROP TABLE IF EXISTS documents_fts;

                CREATE VIRTUAL TABLE documents_fts USING fts5(
                    document_id UNINDEXED,
                    title,
                    file_name,
                    metadata,
                    body,
                    tokenize = 'unicode61',
                    prefix = '2 3 4'
                );

                CREATE TABLE search_documents (
                    document_id INTEGER PRIMARY KEY,
                    document_uid TEXT UNIQUE NOT NULL,
                    archive_number TEXT NOT NULL,
                    title_display TEXT NOT NULL,
                    folder_id TEXT NULL,
                    archive_date TEXT NOT NULL,
                    document_date TEXT NULL,
                    file_type TEXT NULL,
                    status TEXT NOT NULL,
                    is_searchable INTEGER NOT NULL DEFAULT 1,
                    indexed_at INTEGER NOT NULL,
                    content_hash TEXT NULL,
                    index_status TEXT NOT NULL,
                    error_code TEXT NULL
                );

                CREATE INDEX ix_search_documents_folder ON search_documents (folder_id);
                CREATE INDEX ix_search_documents_status ON search_documents (status);
                CREATE INDEX ix_search_documents_file_type ON search_documents (file_type);
                CREATE INDEX ix_search_documents_archive_date ON search_documents (archive_date);

                CREATE TABLE search_document_tags (
                    document_id INTEGER NOT NULL,
                    tag_id TEXT NOT NULL,
                    PRIMARY KEY (document_id, tag_id)
                );

                CREATE TABLE search_state (
                    document_uid TEXT PRIMARY KEY,
                    source_updated_at INTEGER NOT NULL,
                    normalizer_version INTEGER NOT NULL,
                    indexed_at INTEGER NOT NULL
                );
                """,
                transaction: transaction, cancellationToken: ct));

            return true;
        }, cancellationToken);

        availability.MarkAvailable();

        // An empty search_state makes every active archive.db document look "missing from
        // the index" to the reconciliation diff — running it now is a full re-index, reusing
        // the exact same tested logic as the periodic sweep rather than a separate code path.
        await reconciliationService.RunFullSweepAsync(cancellationToken);

        logger.LogInformation("Search index rebuild completed");
    }
}
