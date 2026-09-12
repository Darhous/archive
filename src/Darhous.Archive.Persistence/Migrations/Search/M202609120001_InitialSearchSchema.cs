using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Search;

/// <summary>
/// DB Spec §86-89 (search.db). Fully disposable/rebuildable — never referenced by a real
/// SQLite foreign key to archive.db (cross-file FKs aren't possible anyway; Phase 2 design
/// review). <c>documents_fts.rowid</c> is deliberately kept equal to
/// <c>search_documents.document_id</c> (both are SQLite `INTEGER PRIMARY KEY` aliases for
/// rowid) so the indexer can update/delete FTS rows by id without a lookup join — the
/// standard "external content" FTS5 pairing pattern.
///
/// <c>metadata</c>/<c>body</c> columns and <c>search_document_tags</c> exist per spec now so
/// no future migration is needed, but Phase 8 has nothing to put in them yet: there is no
/// metadata/custom-fields module and no OCR/text-extraction pipeline (those are later
/// phases). The indexer writes empty strings there until those features exist — the same
/// "schema ready, feature later" pattern already used for `tags`/`document_tags` in Phase 2.
/// </summary>
[Tags("Search")]
[Migration(202609120001)]
public sealed class M202609120001_InitialSearchSchema : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
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
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            DROP TABLE IF EXISTS search_state;
            DROP TABLE IF EXISTS search_document_tags;
            DROP TABLE IF EXISTS search_documents;
            DROP TABLE IF EXISTS documents_fts;
            """);
    }
}
