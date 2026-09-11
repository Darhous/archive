using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Audit;

/// <summary>
/// DB Spec §82 (audit_events). Lives in audit.db, entirely separate from archive.db — no
/// real SQLite FK to app_users, since SQLite cannot enforce foreign keys across database
/// files (Phase 2 design review). `username_snapshot`/`role_snapshot`/`entity_name_snapshot`
/// exist specifically so the audit trail stays readable even after the referenced user or
/// entity is gone — this was already the documented design intent, not a Phase 4 addition.
/// </summary>
[Tags("Audit")]
[Migration(202609110001)]
public sealed class M202609110001_InitialAuditSchema : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            CREATE TABLE audit_events (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                timestamp INTEGER NOT NULL,
                user_id INTEGER NULL,
                username_snapshot TEXT NULL,
                role_snapshot TEXT NULL,
                action TEXT NOT NULL,
                action_category TEXT NOT NULL,
                entity_type TEXT NULL,
                entity_uid TEXT NULL,
                entity_name_snapshot TEXT NULL,
                details TEXT NULL,
                search_query TEXT NULL,
                sort_expression TEXT NULL,
                filter_expression TEXT NULL,
                before_json TEXT NULL,
                after_json TEXT NULL,
                result TEXT NOT NULL CHECK (result IN ('success', 'failure')),
                error_code TEXT NULL,
                correlation_id TEXT NULL,
                job_uid TEXT NULL,
                plugin_id TEXT NULL,
                machine_name TEXT NOT NULL,
                created_at INTEGER NOT NULL
            );

            CREATE INDEX ix_audit_events_timestamp ON audit_events (timestamp DESC);
            CREATE INDEX ix_audit_events_user_timestamp ON audit_events (user_id, timestamp DESC);
            CREATE INDEX ix_audit_events_action_timestamp ON audit_events (action, timestamp DESC);
            CREATE INDEX ix_audit_events_entity_timestamp ON audit_events (entity_uid, timestamp DESC);
            CREATE INDEX ix_audit_events_correlation_id ON audit_events (correlation_id);
            """);
    }

    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS audit_events;");
    }
}
