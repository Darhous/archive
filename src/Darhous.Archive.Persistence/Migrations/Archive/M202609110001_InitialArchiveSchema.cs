using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

/// <summary>
/// DB Spec §17-71 (archive.db tables) — Implementation Plan §33's "Database First Migration"
/// list (roles, app_users, documents, document_versions, folders, tags, document_tags, jobs,
/// outbox_events, app_settings) plus the `schema_migrations` table FluentMigrator itself owns.
///
/// Written as raw SQL (<see cref="Execute.Sql"/>) rather than FluentMigrator's fluent
/// Create.Table API: SQLite's DDL model (inline FK clauses at CREATE TABLE time, no
/// ALTER TABLE ADD CONSTRAINT, no dropping/renaming columns) doesn't map cleanly onto
/// FluentMigrator's cross-engine fluent surface — raw SQL that can be diffed directly
/// against the DB Spec tables is safer here than fighting the abstraction (Phase 2 design
/// review, corroborated by all three reviewers — see docs/EXECUTION_PLAN.md).
///
/// ON DELETE actions follow DB Spec §107.1 (ON DELETE Policy): CASCADE for data that only
/// exists as part of its parent (versions/tags/document_tags), SET NULL for
/// documents.folder_id (deleting a folder un-classifies its documents, doesn't delete
/// them), RESTRICT everywhere else (created_by/updated_by/role_id/user_id — an audit-trail
/// style reference should never silently vanish; the app is expected to reassign or
/// soft-delete instead — SAD §178 "User Deletion").
/// </summary>
[Tags("Archive")]
[Migration(202609110001)]
public sealed class M202609110001_InitialArchiveSchema : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            CREATE TABLE roles (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                code TEXT UNIQUE NOT NULL,
                display_name TEXT NOT NULL,
                is_system INTEGER NOT NULL CHECK (is_system IN (0, 1)),
                created_at INTEGER NOT NULL
            );
            """);

        Execute.Sql(
            """
            CREATE TABLE app_users (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                username TEXT UNIQUE NOT NULL,
                display_name TEXT NOT NULL,
                password_hash TEXT NOT NULL,
                password_scheme TEXT NOT NULL,
                role_id INTEGER NOT NULL
                    REFERENCES roles (id) ON DELETE RESTRICT,
                is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
                must_change_password INTEGER NOT NULL DEFAULT 0 CHECK (must_change_password IN (0, 1)),
                failed_login_count INTEGER NOT NULL DEFAULT 0,
                locked_until INTEGER NULL,
                last_login_at INTEGER NULL,
                created_at INTEGER NOT NULL,
                updated_at INTEGER NOT NULL
            );
            """);

        Execute.Sql(
            """
            CREATE TABLE folders (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                parent_id INTEGER NULL
                    REFERENCES folders (id) ON DELETE RESTRICT,
                name TEXT NOT NULL,
                name_normalized TEXT NOT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0,
                icon_key TEXT NULL,
                is_system INTEGER NOT NULL DEFAULT 0 CHECK (is_system IN (0, 1)),
                is_hidden INTEGER NOT NULL DEFAULT 0 CHECK (is_hidden IN (0, 1)),
                created_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE RESTRICT,
                created_at INTEGER NOT NULL,
                updated_at INTEGER NOT NULL
            );

            -- DB Spec §34: UNIQUE(parent_id, name_normalized) as a literal column
            -- constraint does not work in SQLite because every NULL is considered
            -- distinct, which would let unlimited duplicate root-level folder names
            -- through. COALESCE(parent_id, 0) collapses every "no parent" row onto the
            -- same key so the uniqueness check actually applies at the root too.
            CREATE UNIQUE INDEX ux_folders_sibling_name
                ON folders (COALESCE(parent_id, 0), name_normalized);

            CREATE INDEX ix_folders_parent_sort ON folders (parent_id, sort_order);
            """);

        Execute.Sql(
            """
            CREATE TABLE documents (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                archive_number TEXT UNIQUE NOT NULL,
                title TEXT NOT NULL,
                title_normalized TEXT NOT NULL,
                folder_id INTEGER NULL
                    REFERENCES folders (id) ON DELETE SET NULL,
                current_version_id INTEGER NULL
                    REFERENCES document_versions (id) ON DELETE RESTRICT,
                status TEXT NOT NULL CHECK (status IN (
                    'active', 'processing', 'needs_review', 'needs_ocr',
                    'index_failed', 'missing', 'trashed', 'quarantined'
                )),
                source_type TEXT NOT NULL CHECK (source_type IN (
                    'scan', 'import', 'watch_folder', 'manual', 'outlook', 'plugin', 'migration'
                )),
                storage_mode TEXT NOT NULL CHECK (storage_mode IN ('managed', 'indexed_in_place')),
                document_date TEXT NULL,
                scan_date TEXT NULL,
                archive_date TEXT NOT NULL,
                created_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE RESTRICT,
                created_at INTEGER NOT NULL,
                updated_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE RESTRICT,
                updated_at INTEGER NOT NULL,
                deleted_at INTEGER NULL
            );

            CREATE INDEX ix_documents_folder_status ON documents (folder_id, status);
            CREATE INDEX ix_documents_archive_date ON documents (archive_date);
            CREATE INDEX ix_documents_document_date ON documents (document_date);
            CREATE INDEX ix_documents_updated_at ON documents (updated_at);
            CREATE INDEX ix_documents_title_normalized ON documents (title_normalized);
            """);

        Execute.Sql(
            """
            CREATE TABLE document_versions (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                document_id INTEGER NOT NULL
                    REFERENCES documents (id) ON DELETE CASCADE,
                version_no INTEGER NOT NULL,
                original_file_name TEXT NOT NULL,
                stored_file_name TEXT NOT NULL,
                file_path TEXT NOT NULL,
                file_extension TEXT NOT NULL,
                mime_type TEXT NULL,
                file_size INTEGER NOT NULL,
                sha256 TEXT NOT NULL,
                page_count INTEGER NULL,
                file_created_at INTEGER NULL,
                file_modified_at INTEGER NULL,
                imported_at INTEGER NOT NULL,
                created_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE RESTRICT,
                availability_status TEXT NOT NULL CHECK (availability_status IN (
                    'available', 'missing', 'corrupt', 'offline'
                )),
                is_searchable_pdf INTEGER NULL CHECK (is_searchable_pdf IN (0, 1)),
                ocr_provider TEXT NULL,
                content_extraction_status TEXT NOT NULL,
                notes TEXT NULL,
                UNIQUE (document_id, version_no)
            );

            CREATE INDEX ix_document_versions_document_id ON document_versions (document_id);
            CREATE INDEX ix_document_versions_sha256 ON document_versions (sha256);
            CREATE INDEX ix_document_versions_file_path ON document_versions (file_path);
            CREATE INDEX ix_document_versions_availability_status ON document_versions (availability_status);
            """);

        Execute.Sql(
            """
            CREATE TABLE tags (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                name TEXT NOT NULL,
                name_normalized TEXT UNIQUE NOT NULL,
                color_key TEXT NULL,
                created_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE RESTRICT,
                created_at INTEGER NOT NULL
            );
            """);

        Execute.Sql(
            """
            CREATE TABLE document_tags (
                document_id INTEGER NOT NULL
                    REFERENCES documents (id) ON DELETE CASCADE,
                tag_id INTEGER NOT NULL
                    REFERENCES tags (id) ON DELETE CASCADE,
                added_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE RESTRICT,
                added_at INTEGER NOT NULL,
                PRIMARY KEY (document_id, tag_id)
            );

            CREATE INDEX ix_document_tags_tag_document ON document_tags (tag_id, document_id);
            """);

        Execute.Sql(
            """
            CREATE TABLE jobs (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                job_type TEXT NOT NULL,
                owner_component TEXT NOT NULL,
                plugin_id TEXT NULL,
                user_id INTEGER NULL
                    REFERENCES app_users (id) ON DELETE RESTRICT,
                status TEXT NOT NULL CHECK (status IN (
                    'pending', 'running', 'succeeded', 'failed', 'retrying', 'cancelled', 'needs_review'
                )),
                progress_percent REAL NOT NULL DEFAULT 0,
                payload_json TEXT NULL,
                result_json TEXT NULL,
                error_code TEXT NULL,
                error_message TEXT NULL,
                retry_count INTEGER NOT NULL DEFAULT 0,
                max_retries INTEGER NOT NULL DEFAULT 0,
                priority INTEGER NOT NULL DEFAULT 0,
                correlation_id TEXT NULL,
                created_at INTEGER NOT NULL,
                started_at INTEGER NULL,
                completed_at INTEGER NULL,
                next_retry_at INTEGER NULL
            );

            CREATE INDEX ix_jobs_status_priority_created ON jobs (status, priority, created_at);
            CREATE INDEX ix_jobs_next_retry_at ON jobs (next_retry_at);
            CREATE INDEX ix_jobs_correlation_id ON jobs (correlation_id);
            """);

        Execute.Sql(
            """
            CREATE TABLE outbox_events (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                event_type TEXT NOT NULL,
                delivery_level TEXT NOT NULL CHECK (delivery_level IN ('reliable', 'critical')),
                payload_json TEXT NOT NULL,
                correlation_id TEXT NULL,
                user_id INTEGER NULL
                    REFERENCES app_users (id) ON DELETE RESTRICT,
                status TEXT NOT NULL CHECK (status IN (
                    'pending', 'processing', 'processed', 'failed', 'dead_letter'
                )),
                retry_count INTEGER NOT NULL DEFAULT 0,
                created_at INTEGER NOT NULL,
                processed_at INTEGER NULL,
                next_retry_at INTEGER NULL,
                last_error TEXT NULL
            );

            CREATE INDEX ix_outbox_events_status_next_retry ON outbox_events (status, next_retry_at);
            CREATE INDEX ix_outbox_events_type_created ON outbox_events (event_type, created_at);
            """);

        Execute.Sql(
            """
            CREATE TABLE app_settings (
                setting_key TEXT PRIMARY KEY,
                value_json TEXT NOT NULL,
                updated_by INTEGER NULL
                    REFERENCES app_users (id) ON DELETE RESTRICT,
                updated_at INTEGER NOT NULL
            );
            """);
    }

    public override void Down()
    {
        // Reverse dependency order.
        Execute.Sql("DROP TABLE IF EXISTS app_settings;");
        Execute.Sql("DROP TABLE IF EXISTS outbox_events;");
        Execute.Sql("DROP TABLE IF EXISTS jobs;");
        Execute.Sql("DROP TABLE IF EXISTS document_tags;");
        Execute.Sql("DROP TABLE IF EXISTS tags;");
        Execute.Sql("DROP TABLE IF EXISTS document_versions;");
        Execute.Sql("DROP TABLE IF EXISTS documents;");
        Execute.Sql("DROP TABLE IF EXISTS folders;");
        Execute.Sql("DROP TABLE IF EXISTS app_users;");
        Execute.Sql("DROP TABLE IF EXISTS roles;");
    }
}
