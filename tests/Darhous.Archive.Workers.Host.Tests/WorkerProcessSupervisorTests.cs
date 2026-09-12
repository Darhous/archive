using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Darhous.Archive.Contracts.Health;
using Darhous.Archive.Core.Health;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Workers.Host;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Darhous.Archive.Workers.Host.Tests;

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FakeDelayProvider : IWorkerDelayProvider
{
    public List<TimeSpan> RequestedDelays { get; } = new();
    
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        RequestedDelays.Add(delay);
        return Task.CompletedTask;
    }
}

public sealed class FakeHealthRegistry : IHealthRegistry
{
    public List<IHealthContributor> Registered { get; } = new();

    public Task<IReadOnlyList<HealthReport>> CheckAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<HealthReport>>(Array.Empty<HealthReport>());
    }

    public void Register(IHealthContributor contributor)
    {
        Registered.Add(contributor);
    }

    public void Unregister(IHealthContributor contributor)
    {
        Registered.Remove(contributor);
    }
}

public class WorkerProcessSupervisorTests : IAsyncLifetime, IDisposable
{
    private readonly string _testWorkerPath;
    private readonly FakeClock _clock;
    private readonly FakeDelayProvider _delayProvider;
    private readonly FakeHealthRegistry _healthRegistry;
    private readonly WorkerSupervisionOptions _options;
    
    public WorkerProcessSupervisorTests()
    {
        var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
        var ext = isWindows ? ".exe" : "";
        _testWorkerPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "Fixtures", "Darhous.TestWorkerProcess", "bin", "Debug", "net10.0", $"Darhous.TestWorkerProcess{ext}"));
            
        _clock = new FakeClock();
        _delayProvider = new FakeDelayProvider();
        _healthRegistry = new FakeHealthRegistry();
        _options = new WorkerSupervisionOptions();
    }
    
    public Task InitializeAsync()
    {
        if (!File.Exists(_testWorkerPath))
        {
            throw new FileNotFoundException($"Test worker not found at {_testWorkerPath}");
        }
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }

    private WorkerProcessSupervisor CreateSupervisor()
    {
        return new WorkerProcessSupervisor(
            _testWorkerPath,
            _options,
            _delayProvider,
            _clock,
            _healthRegistry,
            NullLogger<WorkerProcessSupervisor>.Instance);
    }

    [Fact]
    public async Task CleanStart_ReportsHealthy_NoRestarts()
    {
        _options.EnvironmentVariables["TESTWORKER_CRASH"] = "0";
        
        await using var supervisor = CreateSupervisor();
        await supervisor.StartAsync();
        
        for (int i = 0; i < 200; i++)
        {
            if (supervisor.CurrentStatus == "healthy") break;
            await Task.Delay(50);
        }
        
        Assert.Equal("healthy", supervisor.CurrentStatus);
        
        var health = await supervisor.CheckAsync(default);
        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Empty(_delayProvider.RequestedDelays);
    }

    [Fact]
    public async Task OneCrash_RestartAttempt1FiresImmediately()
    {
        // Use a threshold > 1 to avoid quarantine on first crash
        var options = new WorkerSupervisionOptions { CrashLoopThreshold = 5 };
        options.EnvironmentVariables["TESTWORKER_CRASH"] = "1";
        var supervisor = new WorkerProcessSupervisor(_testWorkerPath, options, _delayProvider, _clock, _healthRegistry, NullLogger<WorkerProcessSupervisor>.Instance);
        
        await supervisor.StartAsync();
        
        for (int i = 0; i < 200; i++)
        {
            if (_delayProvider.RequestedDelays.Count > 0) break;
            await Task.Delay(50);
        }
        
        Assert.Contains(TimeSpan.Zero, _delayProvider.RequestedDelays);
        
        await supervisor.DisposeAsync();
    }

    [Fact]
    public async Task RepeatedCrashes_DelaysCorrectlyRequested_ThenFails()
    {
        var customOptions = new WorkerSupervisionOptions
        {
            FirstRestartDelay = TimeSpan.Zero,
            SecondRestartDelay = TimeSpan.FromSeconds(5),
            ThirdRestartDelay = TimeSpan.FromSeconds(30),
            CrashLoopThreshold = 100 // Avoid quarantine
        };
        customOptions.EnvironmentVariables["TESTWORKER_CRASH"] = "1";
        
        var supervisor = new WorkerProcessSupervisor(
            _testWorkerPath,
            customOptions,
            _delayProvider,
            _clock,
            _healthRegistry,
            NullLogger<WorkerProcessSupervisor>.Instance);
            
        await supervisor.StartAsync();
        
        for (int i = 0; i < 200; i++)
        {
            if (supervisor.CurrentStatus == "failed") break;
            await Task.Delay(50);
        }
        
        Assert.Equal("failed", supervisor.CurrentStatus);
        
        var expectedDelays = new[] 
        { 
            TimeSpan.Zero, 
            TimeSpan.FromSeconds(5), 
            TimeSpan.FromSeconds(30) 
        };
        
        Assert.Equal(expectedDelays, _delayProvider.RequestedDelays);
        
        await supervisor.DisposeAsync();
    }

    [Fact]
    public async Task ThreeCrashesWithinWindow_Quarantined()
    {
        _options.EnvironmentVariables["TESTWORKER_CRASH"] = "1";
        
        await using var supervisor = CreateSupervisor();
        await supervisor.StartAsync();
        
        for (int i = 0; i < 200; i++)
        {
            if (supervisor.CurrentStatus == "quarantined") break;
            await Task.Delay(50);
        }
        
        Assert.Equal("quarantined", supervisor.CurrentStatus);
        
        Assert.Equal(2, _delayProvider.RequestedDelays.Count);
        Assert.Equal(TimeSpan.Zero, _delayProvider.RequestedDelays[0]);
        Assert.Equal(TimeSpan.FromSeconds(5), _delayProvider.RequestedDelays[1]);
        
        var health = await supervisor.CheckAsync(default);
        Assert.Equal(HealthStatus.Failed, health.Status);
    }
}
