using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

/// <summary>DB Spec #78 - Phase 19 update execution history.</summary>
[Tags("Archive")]
[Migration(202609120008)]
public sealed class M202609120008_UpdateHistory : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            CREATE TABLE update_history (
                id INTEGER PRIMARY KEY,
                component_type TEXT NOT NULL,
                component_id TEXT NOT NULL,
                from_version TEXT NULL,
                to_version TEXT NOT NULL,
                status TEXT NOT NULL,
                initiated_by INTEGER NULL REFERENCES app_users (id) ON DELETE SET NULL,
                started_at INTEGER NOT NULL,
                completed_at INTEGER NULL,
                rollback_version TEXT NULL,
                error_message TEXT NULL
            );

            CREATE INDEX ix_update_history_component_started
                ON update_history (component_type, component_id, started_at DESC);
            CREATE INDEX ix_update_history_status ON update_history (status);
            """);
    }

    public override void Down() => Delete.Table("update_history");
}
