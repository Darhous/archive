using Darhous.Archive.Contracts.Health;
using Darhous.Archive.Core.Health;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Health;

/// <summary>Implements Phase 1's <see cref="IHealthContributor"/> for one SQLite database file.</summary>
public sealed class SqliteDatabaseHealthContributor(DatabaseKind database, ISqliteConnectionFactory connectionFactory)
    : IHealthContributor
{
    public string Component => database switch
    {
        DatabaseKind.Archive => "Database",
        DatabaseKind.Audit => "Audit Log",
        DatabaseKind.Search => "Search Index",
        _ => database.ToString(),
    };

    public async Task<HealthReport> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await connectionFactory.OpenAsync(database, cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthReport.Healthy(Component);
        }
        catch (Exception ex)
        {
            return HealthReport.Failed(Component, ex.Message);
        }
    }
}
