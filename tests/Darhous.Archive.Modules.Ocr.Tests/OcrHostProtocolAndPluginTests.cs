using Darhous.Archive.Contracts.Health;
using Darhous.Archive.Core.Health;
using Darhous.Archive.Modules.Ocr;
using Darhous.Archive.Ocr.Worker;
using Darhous.Archive.PluginSdk.Configuration;
using Darhous.Archive.PluginSdk.Runtime;
using Darhous.Archive.Workers;
using Darhous.Archive.Workers.Protocol;
using Darhous.Archive.Workers.Transport;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Workers.Host;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Modules.Ocr.Tests;

public sealed class OcrHostProtocolAndPluginTests
{
    [Fact]
    public async Task HostClient_InteroperatesWithFakeEngineWorkerAndDefaultsToArabic()
    {
        var root = Path.Combine(Path.GetTempPath(), "Darhous.Ocr.Host.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Path.Combine(root, "outside-original.pdf");
        await File.WriteAllTextAsync(original, "%PDF original");
        var options = new WorkerProtocolOptions { ManagedTempStorageRoot = Path.Combine(root, "managed") };
        Directory.CreateDirectory(options.ManagedTempStorageRoot);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var engine = new FakeEngine();
        var pipeName = NamedPipeTransport.CreateUniquePipeName(options);
        await using var host = NamedPipeTransport.CreateServer(pipeName, options);
        var connectionTask = NamedPipeTransport.ConnectClientAsync(pipeName, options, timeout.Token);
        await Task.WhenAll(host.WaitForConnectionAsync(timeout.Token), connectionTask);
        await using var worker = await connectionTask;
        var handshake = new WorkerHandshakeHost(options);
        var session = new OcrWorkerSession(engine, options).RunAsync(worker, handshake.SessionToken, timeout.Token);
        await handshake.AcceptAsync(host, OcrWorkerSession.WorkerId, timeout.Token);
        var client = new OcrWorkerClient(host, options);

        var result = await client.ProcessAsync(original, cancellationToken: timeout.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("host-worker text", result.ExtractedText);
        Assert.Equal(["ara"], engine.Languages);
        Assert.Equal("%PDF original", await File.ReadAllTextAsync(original));
        await timeout.CancelAsync();
        try { await session; } catch (OperationCanceledException) { }
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Plugin_RegistersProviderAndForwardsLifecycle()
    {
        var provider = new FakeManagedProvider();
        var plugin = new OcrPlugin(provider);
        var services = new CapturingServices();
        var configuration = new ConfigurationContext(services);

        await plugin.ConfigureAsync(configuration, CancellationToken.None);
        Assert.Same(provider, services.Provider);
        await plugin.StartAsync(null!, CancellationToken.None);
        await plugin.StopAsync(CancellationToken.None);

        Assert.Equal(1, provider.StartCount);
        Assert.Equal(1, provider.StopCount);
    }

    [Fact]
    public async Task SupervisedRealWorker_HandshakesAndReportsUnavailableEngineWithoutCrashing()
    {
        var root = Path.Combine(Path.GetTempPath(), "Darhous.Ocr.Process.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var input = Path.Combine(root, "input.pdf");
        await File.WriteAllTextAsync(input, "%PDF-1.4 unavailable-engine test");
        var worker = Path.Combine(AppContext.BaseDirectory, "Darhous.Archive.Ocr.Worker.exe");
        Assert.True(File.Exists(worker));
        var supervision = new OcrSupervisionOptions
        {
            ExecutablePath = worker,
            ArchiveStorageRoot = Path.Combine(root, "archive"),
            Protocol = new WorkerProtocolOptions
            {
                PipeNamePrefix = $"Darhous.Ocr.Process.Tests.{Guid.NewGuid():N}",
                ManagedTempStorageRoot = Path.Combine(root, "managed"),
                HandshakeTimeout = TimeSpan.FromSeconds(15),
            },
            WorkerSupervision = new WorkerSupervisionOptions
            {
                EnvironmentVariables = new()
                {
                    ["DARHOUS_TESSDATA_PATH"] = Path.Combine(root, "deliberately-missing-tessdata"),
                },
            },
        };
        await using var provider = new WorkerOcrProvider(
            supervision,
            new NoopHealthRegistry(),
            new SystemClock(),
            new DefaultWorkerDelayProvider(),
            NullLoggerFactory.Instance);

        await provider.StartAsync(CancellationToken.None);
        var result = await provider.ProcessAsync(input, ["ara"], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ocr_engine_unavailable", result.FailureCode);
        await provider.StopAsync(CancellationToken.None);
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
    }

    private sealed class FakeEngine : IOcrEngine
    {
        public OcrEngineAvailability Availability { get; } = new(true);
        public IReadOnlyList<string>? Languages { get; private set; }
        public async Task<OcrEngineResult> ProcessAsync(string inputPdfPath, string outputPdfPath, IReadOnlyList<string> languages, CancellationToken cancellationToken)
        {
            Languages = languages.ToArray();
            await File.WriteAllTextAsync(outputPdfPath, "%PDF searchable", cancellationToken);
            return new("host-worker text", outputPdfPath, null);
        }
        public void Dispose() { }
    }

    private sealed class FakeManagedProvider : IManagedOcrProvider
    {
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken) { StartCount++; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken) { StopCount++; return Task.CompletedTask; }
        public Task<OcrResult> ProcessAsync(string inputFilePath, IReadOnlyList<string>? languages = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(OcrResult.Failed("unused", "unused"));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CapturingServices : IServiceRegistry
    {
        public IOcrProvider? Provider { get; private set; }
        public void AddSingleton<TService>(TService instance) where TService : class
        {
            if (instance is IOcrProvider provider) Provider = provider;
        }
        public void AddSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService =>
            throw new NotSupportedException();
    }

    private sealed class ConfigurationContext(IServiceRegistry services) : IPluginConfigurationContext
    {
        public IServiceRegistry Services { get; } = services;
        public IEventSubscriptionRegistry Events => null!;
        public IUiExtensionRegistry Ui => null!;
        public IBackgroundTaskRegistry BackgroundTasks => null!;
        public IHealthRegistry Health { get; } = new NoopHealthRegistry();
    }

    private sealed class NoopHealthRegistry : IHealthRegistry
    {
        public Task<IReadOnlyList<HealthReport>> CheckAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<HealthReport>>([]);
        public void Register(IHealthContributor contributor) { }
        public void Unregister(IHealthContributor contributor) { }
    }
}
