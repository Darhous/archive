using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Tests;

public class InitialMigrationTests : PersistenceTestBase
{
    private static readonly string[] ExpectedArchiveTables =
    [
        "roles", "app_users", "folders", "documents", "document_versions",
        "tags", "document_tags", "jobs", "outbox_events", "app_settings", "schema_migrations",
        "user_sessions", // M202609110002_UserSessions (Phase 3)
    ];

    [Fact]
    public async Task ArchiveDb_HasAllTablesFromFirstMigration()
    {
        var tables = await GetTableNamesAsync(DatabaseKind.Archive);

        foreach (var expected in ExpectedArchiveTables)
        {
            Assert.Contains(expected, tables);
        }
    }

    [Fact]
    public async Task AuditDb_And_SearchDb_HaveNoArchiveTablesLeaked()
    {
        // Safety net for the tag-filtering design (MigrationRunnerFactory + PersistenceInitializer's
        // DatabasesWithMigrations list): if the "Archive" tag were ever misconfigured, or if
        // Audit/Search were mistakenly added to DatabasesWithMigrations before they have their
        // own migrations, this is what would catch an archive table silently appearing in the
        // wrong database file. They have zero tables in Phase 2 — their real schemas arrive in
        // Phase 4 (Audit) and Phase 8 (Search).
        var auditTables = await GetTableNamesAsync(DatabaseKind.Audit);
        var searchTables = await GetTableNamesAsync(DatabaseKind.Search);

        Assert.Empty(auditTables);
        Assert.Empty(searchTables);
    }

    [Fact]
    public async Task ArchiveDb_HasExpectedIndexes()
    {
        var indexes = await GetIndexNamesAsync(DatabaseKind.Archive);

        Assert.Contains("ux_folders_sibling_name", indexes);
        Assert.Contains("ix_documents_folder_status", indexes);
        Assert.Contains("ix_document_versions_sha256", indexes);
        Assert.Contains("ix_outbox_events_status_next_retry", indexes);
        Assert.Contains("ix_jobs_status_priority_created", indexes);
        Assert.Contains("ux_user_sessions_token_hash", indexes);
    }

    [Fact]
    public async Task ArchiveDb_HasFourSeededSystemRoles()
    {
        var factory = new SqliteConnectionFactory(Options);
        await using var connection = await factory.OpenAsync(DatabaseKind.Archive, CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT code FROM roles ORDER BY id;";

        var codes = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            codes.Add(reader.GetString(0));
        }

        Assert.Equal(["admin", "user", "readonly", "guest"], codes);
    }

    [Fact]
    public async Task Migrations_AreIdempotent_RunningTwiceDoesNotFail()
    {
        // PersistenceInitializer already ran once in InitializeAsync(); running it again
        // must be a no-op (FluentMigrator skips already-applied versions), not a crash.
        await PersistenceInitializer.InitializeAsync(Options, CancellationToken.None);

        var tables = await GetTableNamesAsync(DatabaseKind.Archive);
        Assert.Contains("documents", tables);
    }

    private async Task<List<string>> GetTableNamesAsync(DatabaseKind database)
    {
        var factory = new SqliteConnectionFactory(Options);
        await using var connection = await factory.OpenAsync(database, CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private async Task<List<string>> GetIndexNamesAsync(DatabaseKind database)
    {
        var factory = new SqliteConnectionFactory(Options);
        await using var connection = await factory.OpenAsync(database, CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND name NOT LIKE 'sqlite_%';";

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
