using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Events;
using Darhous.Archive.Core.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Darhous.Archive.Persistence.Tests;

/// <summary>Proves the actual composition root (<c>AddPersistence</c>) works end to end, not just the individual pieces.</summary>
public class ServiceCollectionExtensionsTests : PersistenceTestBase
{
    private async Task<IHost> BuildAndStartHostAsync()
    {
        await PersistenceInitializer.InitializeAsync(Options, CancellationToken.None);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddPersistence(Options);

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    [Fact]
    public async Task AddPersistence_ResolvesUnitOfWork_AndCanWriteThroughIt()
    {
        using var host = await BuildAndStartHostAsync();

        var unitOfWork = host.Services.GetRequiredService<IUnitOfWork>();
        var uid = await unitOfWork.ExecuteAsync(
            (context, ct) => context.Roles.CreateAsync("smoke-test", "Smoke Test", false, ct),
            CancellationToken.None);

        var roles = host.Services.GetRequiredService<IRoleRepository>();
        var role = await roles.GetByUidAsync(uid, CancellationToken.None);

        Assert.NotNull(role);
        Assert.Equal("smoke-test", role!.Code);

        await host.StopAsync();
    }

    [Fact]
    public async Task AddPersistence_RegistersThreeHealthContributors_AllHealthy()
    {
        using var host = await BuildAndStartHostAsync();

        var contributors = host.Services.GetServices<IHealthContributor>().ToList();
        Assert.Equal(3, contributors.Count);

        foreach (var contributor in contributors)
        {
            var report = await contributor.CheckAsync(CancellationToken.None);
            Assert.Equal(Darhous.Archive.Contracts.Health.HealthStatus.Healthy, report.Status);
        }

        await host.StopAsync();
    }

    [Fact]
    public async Task AddPersistence_EventBus_IsOutboxBacked()
    {
        using var host = await BuildAndStartHostAsync();

        var bus = host.Services.GetRequiredService<IEventBus>();
        Assert.IsType<Outbox.OutboxEventBus>(bus);

        await host.StopAsync();
    }
}
