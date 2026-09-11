using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Migrations;

namespace Darhous.Archive.Persistence;

/// <summary>
/// Must run to completion before the write queues (<see cref="Writes.SqliteWriteQueue"/>,
/// started as <c>IHostedService</c>s) or anything else touches the databases: enables WAL
/// for all three files, then runs migrations for whichever databases actually have any
/// (Migrations §MigrationRunnerFactory). Deliberately not itself an <c>IHostedService</c> —
/// ordering hosted services relative to each other is fragile; the composition root
/// (Desktop's App bootstrap, or a test) awaits this explicitly first.
/// </summary>
public static class PersistenceInitializer
{
    /// <summary>
    /// FluentMigrator throws <c>MissingMigrationsException</c> if a runner's tag filter
    /// matches zero migration classes — it treats "nothing to migrate" as an error, not a
    /// no-op. archive.db (Phase 2) and audit.db (Phase 4) both have real migrations now;
    /// search.db's schema doesn't exist until Phase 8, so calling the migrator for it today
    /// would just throw. Extend this list when that phase adds its first tagged migration.
    /// </summary>
    private static readonly DatabaseKind[] DatabasesWithMigrations = [DatabaseKind.Archive, DatabaseKind.Audit];

    public static async Task InitializeAsync(PersistenceOptions options, CancellationToken cancellationToken)
    {
        var connectionFactory = new SqliteConnectionFactory(options);
        var databaseInitializer = new SqliteDatabaseInitializer(connectionFactory);

        foreach (var database in Enum.GetValues<DatabaseKind>())
        {
            await databaseInitializer.InitializeAsync(database, cancellationToken);

            if (DatabasesWithMigrations.Contains(database))
            {
                MigrationRunnerFactory.MigrateUp(database, options);
            }
        }
    }
}
