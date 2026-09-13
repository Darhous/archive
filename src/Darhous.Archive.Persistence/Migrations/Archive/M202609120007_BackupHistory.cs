using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

/// <summary>DB Spec §76-77 — Phase 18 backup execution history.</summary>
[Tags("Archive")]
[Migration(202609120007)]
public sealed class M202609120007_BackupHistory : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            CREATE TABLE backup_history (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                backup_type TEXT NOT NULL CHECK (backup_type IN ('metadata', 'full', 'configuration')),
                destination TEXT NOT NULL,
                status TEXT NOT NULL CHECK (status IN ('running', 'completed', 'failed')),
                file_name TEXT NULL,
                file_size INTEGER NULL,
                checksum TEXT NULL,
                created_by INTEGER NULL REFERENCES app_users (id) ON DELETE SET NULL,
                started_at INTEGER NOT NULL,
                completed_at INTEGER NULL,
                error_message TEXT NULL
            );

            CREATE INDEX ix_backup_history_started_at ON backup_history (started_at DESC);
            CREATE INDEX ix_backup_history_status ON backup_history (status);
            """);
    }

    public override void Down()
    {
        Delete.Table("backup_history");
    }
}
