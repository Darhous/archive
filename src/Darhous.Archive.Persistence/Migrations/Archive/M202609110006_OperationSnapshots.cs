using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

/// <summary>
/// DB Spec §142 (operation_snapshots) — backs Undo for bulk operations (§141: Bulk Move,
/// Tags Bulk Update). Default 30-second window; never used for Permanent Delete, Restore,
/// or Plugin install (those are intentionally not undoable).
/// </summary>
[Tags("Archive")]
[Migration(202609110006)]
public sealed class M202609110006_OperationSnapshots : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            CREATE TABLE operation_snapshots (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                user_id INTEGER NULL
                    REFERENCES app_users (id) ON DELETE RESTRICT,
                operation_type TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                created_at INTEGER NOT NULL,
                expires_at INTEGER NOT NULL,
                reverted_at INTEGER NULL
            );

            CREATE INDEX ix_operation_snapshots_expires_at ON operation_snapshots (expires_at);
            """);
    }

    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS operation_snapshots;");
    }
}
