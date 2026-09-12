using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

/// <summary>
/// DB Spec §48/§50-53/§58 (storage_roots, source_drives, source_exclusions, discovery_runs,
/// watch_folders) — Phase 9 (Automatic Computer Discovery). All in archive.db: these are
/// configuration/audit-trail tables for what Discovery scans, not the documents themselves.
/// </summary>
[Tags("Archive")]
[Migration(202609120002)]
public sealed class M202609120002_DiscoverySchema : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            CREATE TABLE storage_roots (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                name TEXT NOT NULL,
                root_path TEXT UNIQUE NOT NULL,
                storage_type TEXT NOT NULL CHECK (storage_type IN ('managed', 'external', 'removable')),
                is_default INTEGER NOT NULL DEFAULT 0,
                is_enabled INTEGER NOT NULL DEFAULT 1,
                created_at INTEGER NOT NULL
            );

            CREATE TABLE source_drives (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                drive_root TEXT UNIQUE NOT NULL,
                volume_label TEXT NULL,
                volume_serial TEXT NULL,
                drive_type TEXT NOT NULL CHECK (drive_type IN ('fixed', 'removable', 'network', 'unknown')),
                is_enabled INTEGER NOT NULL DEFAULT 1,
                auto_discover INTEGER NOT NULL DEFAULT 0,
                last_seen_at INTEGER NULL,
                last_discovery_at INTEGER NULL,
                last_reconcile_at INTEGER NULL,
                created_at INTEGER NOT NULL,
                updated_at INTEGER NOT NULL
            );

            CREATE TABLE source_exclusions (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                exclusion_type TEXT NOT NULL CHECK (exclusion_type IN ('drive', 'folder', 'subfolder')),
                path TEXT NOT NULL,
                path_normalized TEXT NOT NULL,
                is_system INTEGER NOT NULL DEFAULT 0,
                is_enabled INTEGER NOT NULL DEFAULT 1,
                reason TEXT NULL,
                created_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE SET NULL,
                created_at INTEGER NOT NULL,
                updated_at INTEGER NOT NULL
            );

            CREATE UNIQUE INDEX ux_source_exclusions_path_normalized ON source_exclusions (path_normalized);
            CREATE INDEX ix_source_exclusions_enabled_system ON source_exclusions (is_enabled, is_system);

            CREATE TABLE discovery_runs (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                run_type TEXT NOT NULL CHECK (run_type IN (
                    'initial_discovery', 'hourly_reconciliation', 'daily_full_reconciliation', 'manual_rescan', 'drive_rescan'
                )),
                status TEXT NOT NULL CHECK (status IN ('pending', 'running', 'paused', 'completed', 'cancelled', 'failed')),
                requested_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE SET NULL,
                started_at INTEGER NOT NULL,
                completed_at INTEGER NULL,
                drives_scanned INTEGER NOT NULL DEFAULT 0,
                files_seen INTEGER NOT NULL DEFAULT 0,
                supported_files INTEGER NOT NULL DEFAULT 0,
                queued_for_index INTEGER NOT NULL DEFAULT 0,
                skipped_by_exclusion INTEGER NOT NULL DEFAULT 0,
                missing_detected INTEGER NOT NULL DEFAULT 0,
                error_count INTEGER NOT NULL DEFAULT 0,
                summary_json TEXT NULL
            );

            CREATE INDEX ix_discovery_runs_status_started ON discovery_runs (status, started_at);

            CREATE TABLE watch_folders (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                path TEXT UNIQUE NOT NULL,
                include_subfolders INTEGER NOT NULL DEFAULT 1,
                import_mode TEXT NOT NULL CHECK (import_mode IN ('managed_copy', 'managed_move', 'index_in_place')),
                destination_folder_id INTEGER NULL
                    REFERENCES folders (id) ON DELETE SET NULL,
                is_enabled INTEGER NOT NULL DEFAULT 1,
                last_reconciled_at INTEGER NULL,
                created_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE SET NULL,
                created_at INTEGER NOT NULL
            );
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            DROP TABLE IF EXISTS watch_folders;
            DROP TABLE IF EXISTS discovery_runs;
            DROP TABLE IF EXISTS source_exclusions;
            DROP TABLE IF EXISTS source_drives;
            DROP TABLE IF EXISTS storage_roots;
            """);
    }
}
