using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Darhous.Archive.Modules.Scanner;
using Darhous.Archive.Modules.Scanner.Persistence;
using Darhous.Archive.PluginSdk.Configuration;
using Darhous.Archive.PluginSdk.Runtime;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Core.Health;
using Darhous.Archive.Workers.Host;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using FluentMigrator.Runner;
using System.Runtime.Versioning;

namespace Darhous.Archive.Modules.Scanner.Tests;

[SupportedOSPlatform("windows")]
public class ScannerPluginTests
{
    private class DummyPluginConfigurationContext : IPluginConfigurationContext
    {
        public DummyPluginConfigurationContext(IServiceRegistry services)
        {
            Services = services;
        }
        public IServiceRegistry Services { get; }
        public IEventSubscriptionRegistry Events => throw new NotImplementedException();
        public IUiExtensionRegistry Ui => throw new NotImplementedException();
        public IBackgroundTaskRegistry BackgroundTasks => throw new NotImplementedException();
        public IHealthRegistry Health { get; } = new DummyHealthRegistry();
    }

    private class DummyHealthRegistry : IHealthRegistry
    {
        public IHealthContributor? Contributor;
        public void Register(IHealthContributor contributor) { Contributor = contributor; }
        public void Unregister(IHealthContributor contributor) { }
        public Task<System.Collections.Generic.IReadOnlyList<Darhous.Archive.Contracts.Health.HealthReport>> CheckAllAsync(CancellationToken cancellationToken) => 
            Task.FromResult<System.Collections.Generic.IReadOnlyList<Darhous.Archive.Contracts.Health.HealthReport>>(new[] { new Darhous.Archive.Contracts.Health.HealthReport("Test", Darhous.Archive.Contracts.Health.HealthStatus.Healthy, "", default) });
    }

    private class DummyPluginRuntimeContext : IPluginRuntimeContext
    {
        public IServiceResolver Services => throw new NotImplementedException();
        public IPluginStorage Storage => throw new NotImplementedException();
        public IPluginSecrets Secrets => throw new NotImplementedException();
        public IPluginLogger Logger => throw new NotImplementedException();
        public IPluginEnvironment Environment => throw new NotImplementedException();
    }

    private class ServiceRegistryAdapter : IServiceRegistry
    {
        public IServiceCollection Collection { get; } = new ServiceCollection();
        public void AddSingleton<TService>(TService instance) where TService : class => Collection.AddSingleton(instance);
        public void AddSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService => Collection.AddSingleton<TService, TImplementation>();
    }

    [Fact]
    public async Task FullPluginLifecycle_Tests()
    {
        // Match whatever configuration (Debug/Release) this very test assembly was built
        // with, rather than hardcoding "Debug" — CI builds/tests in Release and a hardcoded
        // Debug path leaves the fixture unbuildable there (worker fails to start silently).
        var configuration = Path.GetFileName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)))!;
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "Fixtures", "Darhous.TestScannerWorker", "bin", configuration, "net10.0-windows", "Darhous.TestScannerWorker.exe");
        
        var options = new ScannerSupervisionOptions
        {
            ExecutablePath = fixturePath,
            FirstRestartDelay = TimeSpan.Zero
        };

        var plugin = new ScannerPlugin(
            options,
            new SystemClock(),
            NullLogger<ScannerPlugin>.Instance,
            NullLogger<WorkerProcessSupervisor>.Instance
        );

        var registry = new ServiceRegistryAdapter();
        var configContext = new DummyPluginConfigurationContext(registry);
        
        // 1. Configure and verify services
        await plugin.ConfigureAsync(configContext, CancellationToken.None);
        var provider = registry.Collection.BuildServiceProvider();
        var scannerProvider = provider.GetRequiredService<IScannerProvider>();
        var supervisor = provider.GetRequiredService<WorkerProcessSupervisor>();

        // 2. Start Plugin
        var runtimeContext = new DummyPluginRuntimeContext();
        await plugin.StartAsync(runtimeContext, CancellationToken.None);

        // Wait for worker and handshake
        await Task.Delay(3000);
        
        // 3. ScanAsync Success
        var successProfile = new ScannerProfile(1, "success", 300, "Color", false, "Adf", "FullBatch", true);
        var res1 = await scannerProvider.ScanAsync(successProfile, CancellationToken.None);
        Assert.True(res1.Success);
        Assert.NotEmpty(res1.FilePath);
        
        // 4. ScanAsync Failure
        var failProfile = new ScannerProfile(2, "fail", -1, "Color", false, "Adf", "FullBatch", true);
        var res2 = await scannerProvider.ScanAsync(failProfile, CancellationToken.None);
        Assert.False(res2.Success);
        Assert.Contains("Hardware failure simulated", res2.ErrorMessage);
        

        
        // 5. Worker Crash test
        var contributor = ((DummyHealthRegistry)configContext.Health).Contributor;
        var initialHealth = await contributor!.CheckAsync(CancellationToken.None);
        Assert.Equal(Darhous.Archive.Contracts.Health.HealthStatus.Healthy, initialHealth.Status);

        // Kill the worker process directly (not via the supervisor) to prove the Phase 13
        // restart policy reacts to an external crash, not just an explicit StopAsync.
        var proc = System.Diagnostics.Process.GetProcessesByName("Darhous.TestScannerWorker").FirstOrDefault();
        if (proc != null)
        {
            proc.Kill();
            await Task.Delay(2000); // Wait for restart
            var currentStatus = supervisor.CurrentStatus;
            Assert.True(currentStatus != "Faulted");
        }

        await plugin.StopAsync(CancellationToken.None);
    }
    
    [Fact]
    public void SeedProfiles_AreIdempotent_AndExactlyFive()
    {
        var services = new ServiceCollection();
        var connectionString = "Data Source=InMemorySample;Mode=Memory;Cache=Shared";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        
        services.Configure<FluentMigrator.Runner.Initialization.RunnerOptions>(opt => 
        {
            opt.Tags = new[] { "Archive" };
        });
        
        services.AddFluentMigratorCore()
                .ConfigureRunner(rb => rb
                    .AddSQLite()
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(typeof(Darhous.Archive.Persistence.Migrations.Archive.M202609120004_ScannerProfiles).Assembly).For.Migrations())
                .AddLogging(lb => lb.AddFluentMigratorConsole());

        var sp = services.BuildServiceProvider();
        using (var scope = sp.CreateScope())
        {
            var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
            runner.MigrateUp(); // First run
            runner.MigrateUp(); // Idempotency check (no crash)
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM scanner_profiles";
        var count = (long)cmd.ExecuteScalar()!;
        Assert.Equal(5, count);
    }
}
