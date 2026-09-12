using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

/// <summary>DB Spec §66-68 — Phase 12 (Plugin Platform). Lives in archive.db per the DB Spec's own table listing (not a separate plugins.db).</summary>
[Tags("Archive")]
[Migration(202609120003)]
public sealed class M202609120003_PluginSchema : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            CREATE TABLE plugins (
                id INTEGER PRIMARY KEY,
                plugin_id TEXT UNIQUE NOT NULL,
                installed_version TEXT NOT NULL,
                active_version TEXT NOT NULL,
                publisher TEXT NOT NULL,
                trust_level TEXT NOT NULL CHECK (trust_level IN ('official', 'verified', 'thirdparty', 'developer')),
                status TEXT NOT NULL CHECK (status IN (
                    'not_installed', 'installed', 'disabled', 'starting', 'healthy', 'degraded',
                    'failed', 'stopping', 'pending_restart', 'update_available', 'incompatible', 'quarantined'
                )),
                update_channel TEXT NOT NULL DEFAULT 'stable',
                package_hash TEXT NOT NULL,
                crash_count INTEGER NOT NULL DEFAULT 0,
                installed_at INTEGER NOT NULL,
                updated_at INTEGER NOT NULL,
                last_health_at INTEGER NULL
            );

            CREATE TABLE plugin_permissions (
                plugin_id TEXT NOT NULL,
                permission TEXT NOT NULL,
                granted INTEGER NOT NULL DEFAULT 0,
                granted_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE SET NULL,
                granted_at INTEGER NULL,
                PRIMARY KEY (plugin_id, permission)
            );

            CREATE TABLE plugin_settings (
                plugin_id TEXT NOT NULL,
                setting_key TEXT NOT NULL,
                value_json TEXT NOT NULL,
                updated_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE SET NULL,
                updated_at INTEGER NOT NULL,
                PRIMARY KEY (plugin_id, setting_key)
            );
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            DROP TABLE IF EXISTS plugin_settings;
            DROP TABLE IF EXISTS plugin_permissions;
            DROP TABLE IF EXISTS plugins;
            """);
    }
}
