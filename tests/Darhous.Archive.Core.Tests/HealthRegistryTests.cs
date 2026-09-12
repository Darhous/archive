using Darhous.Archive.Contracts.Health;
using Darhous.Archive.Core.Health;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Core.Tests;

public class HealthRegistryTests
{
    private sealed class FakeContributor(string component, Func<HealthReport> check) : IHealthContributor
    {
        public string Component => component;

        public Task<HealthReport> CheckAsync(CancellationToken cancellationToken) => Task.FromResult(check());
    }

    private sealed class ThrowingContributor(string component) : IHealthContributor
    {
        public string Component => component;

        public Task<HealthReport> CheckAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("simulated failure");
    }

    [Fact]
    public async Task CheckAllAsync_AggregatesEveryContributor()
    {
        var contributors = new[]
        {
            new FakeContributor("Database", () => HealthReport.Healthy("Database")),
            new FakeContributor("Search Index", () => HealthReport.Healthy("Search Index")),
        };

        var registry = new HealthRegistry(contributors, NullLogger<HealthRegistry>.Instance);

        var reports = await registry.CheckAllAsync(CancellationToken.None);

        Assert.Equal(2, reports.Count);
        Assert.All(reports, r => Assert.Equal(HealthStatus.Healthy, r.Status));
    }

    [Fact]
    public async Task CheckAllAsync_OneContributorThrows_OthersStillReported()
    {
        IHealthContributor[] contributors =
        [
            new ThrowingContributor("Scanner"),
            new FakeContributor("Backup", () => HealthReport.Healthy("Backup")),
        ];

        var registry = new HealthRegistry(contributors, NullLogger<HealthRegistry>.Instance);

        var reports = await registry.CheckAllAsync(CancellationToken.None);

        Assert.Equal(2, reports.Count);
        Assert.Equal(HealthStatus.Failed, reports.Single(r => r.Component == "Scanner").Status);
        Assert.Equal(HealthStatus.Healthy, reports.Single(r => r.Component == "Backup").Status);
    }

    [Fact]
    public async Task Register_AddsADynamicContributor_IncludedInSubsequentChecks()
    {
        var registry = new HealthRegistry([], NullLogger<HealthRegistry>.Instance);
        var pluginContributor = new FakeContributor("MyPlugin", () => HealthReport.Healthy("MyPlugin"));

        registry.Register(pluginContributor);
        var reports = await registry.CheckAllAsync(CancellationToken.None);

        Assert.Single(reports);
        Assert.Equal("MyPlugin", reports[0].Component);
    }

    [Fact]
    public async Task Unregister_RemovesADynamicContributor_ExcludedFromLaterChecks()
    {
        var registry = new HealthRegistry([], NullLogger<HealthRegistry>.Instance);
        var pluginContributor = new FakeContributor("MyPlugin", () => HealthReport.Healthy("MyPlugin"));
        registry.Register(pluginContributor);

        registry.Unregister(pluginContributor);
        var reports = await registry.CheckAllAsync(CancellationToken.None);

        Assert.Empty(reports);
    }
}
