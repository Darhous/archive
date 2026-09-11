using System.Reflection;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.VersionTableInfo;
using Microsoft.Extensions.DependencyInjection;
using Darhous.Archive.Persistence.Configuration;

namespace Darhous.Archive.Persistence.Migrations;

/// <summary>
/// Builds one fully independent FluentMigrator runner per database — not one shared runner
/// filtering by tag through a single DI container. Each call gets its own
/// <see cref="ServiceProvider"/> scoped to exactly one connection string and one tag
/// (<c>Get-WorkerPlan</c>-style separation from the Phase 2 design review): a
/// misconfigured tag then fails loudly (wrong DB has zero applicable migrations, caught by
/// <c>InitialMigrationTests</c>) rather than silently running an Archive migration
/// against audit.db.
/// </summary>
public static class MigrationRunnerFactory
{
    private static string TagFor(DatabaseKind database) => database switch
    {
        DatabaseKind.Archive => "Archive",
        DatabaseKind.Audit => "Audit",
        DatabaseKind.Search => "Search",
        _ => throw new ArgumentOutOfRangeException(nameof(database)),
    };

    public static void MigrateUp(DatabaseKind database, PersistenceOptions options)
    {
        var connectionString = $"Data Source={options.GetFilePath(database)}";

        using var serviceProvider = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddSQLite()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(Assembly.GetExecutingAssembly()).For.Migrations())
            .Configure<RunnerOptions>(opt => opt.Tags = [TagFor(database)])
            .AddSingleton<IVersionTableMetaData, SchemaMigrationsMetadata>()
            .BuildServiceProvider(validateScopes: false);

        using var scope = serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }
}
