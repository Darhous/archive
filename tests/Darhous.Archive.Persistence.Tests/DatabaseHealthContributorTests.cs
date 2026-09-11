using Darhous.Archive.Contracts.Health;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Health;

namespace Darhous.Archive.Persistence.Tests;

public class DatabaseHealthContributorTests : PersistenceTestBase
{
    [Theory]
    [InlineData(DatabaseKind.Archive, "Database")]
    [InlineData(DatabaseKind.Audit, "Audit Log")]
    [InlineData(DatabaseKind.Search, "Search Index")]
    public async Task CheckAsync_HealthyDatabase_ReportsHealthy(DatabaseKind database, string expectedComponent)
    {
        var contributor = new SqliteDatabaseHealthContributor(database, new SqliteConnectionFactory(Options));

        var report = await contributor.CheckAsync(CancellationToken.None);

        Assert.Equal(expectedComponent, report.Component);
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task CheckAsync_UnreachableDatabase_ReportsFailedInsteadOfThrowing()
    {
        var brokenOptions = new PersistenceOptions
        {
            // A path whose parent directory cannot be created (a file where a directory is
            // expected) reliably makes SQLite fail to open — proving the contributor turns
            // that into a Failed report rather than letting the exception propagate to the
            // Health page and taking the whole health check down (SAD §5.1).
            DataDirectory = Path.Combine(Options.GetType().Assembly.Location, "not-a-directory"),
        };

        var contributor = new SqliteDatabaseHealthContributor(DatabaseKind.Archive, new SqliteConnectionFactory(brokenOptions));

        var report = await contributor.CheckAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Failed, report.Status);
        Assert.NotNull(report.Description);
    }
}
