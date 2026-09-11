using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

/// <summary>
/// DB Spec §148 (File Path Normalization): "يضاف internal column عند التنفيذ:
/// file_path_normalized" — this is that column, added now because Phase 5 (Document Core)
/// is when file paths first get written. Also DB Spec §28 (number_sequences), needed for
/// transactional archive-number generation (SAD §27: numbers, once assigned, are never reused).
/// </summary>
[Tags("Archive")]
[Migration(202609110004)]
public sealed class M202609110004_DocumentPathNormalizationAndNumbering : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            ALTER TABLE document_versions ADD COLUMN file_path_normalized TEXT NOT NULL DEFAULT '';
            CREATE UNIQUE INDEX ux_document_versions_file_path_normalized
                ON document_versions (file_path_normalized)
                WHERE file_path_normalized != '';
            """);

        Execute.Sql(
            """
            CREATE TABLE number_sequences (
                sequence_key TEXT PRIMARY KEY,
                year INTEGER NOT NULL,
                last_value INTEGER NOT NULL,
                updated_at INTEGER NOT NULL
            );
            """);
    }

    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS number_sequences;");
        Execute.Sql("DROP INDEX IF EXISTS ux_document_versions_file_path_normalized;");
        // SQLite cannot drop a column pre-3.35 without a table rebuild; not needed for Down
        // in practice (Down is only ever exercised in dev/test against a disposable DB).
    }
}
