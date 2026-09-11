using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

/// <summary>DB Spec §46 (recycle_bin_entries) — expires_at defaults NULL: deletion is never automatically permanent in V1.</summary>
[Tags("Archive")]
[Migration(202609110005)]
public sealed class M202609110005_RecycleBinEntries : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            CREATE TABLE recycle_bin_entries (
                document_id INTEGER PRIMARY KEY
                    REFERENCES documents (id) ON DELETE CASCADE,
                original_folder_id INTEGER NULL
                    REFERENCES folders (id) ON DELETE SET NULL,
                deleted_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE RESTRICT,
                deleted_at INTEGER NOT NULL,
                delete_reason TEXT NULL,
                expires_at INTEGER NULL
            );
            """);
    }

    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS recycle_bin_entries;");
    }
}
