using System.IO.Compression;
using Darhous.Archive.Scanner.Worker;
using Darhous.Archive.Workers;
using Darhous.Archive.Workers.Protocol;
using Darhous.Archive.Workers.Transport;

namespace Darhous.Archive.Scanner.Worker.Tests;

public sealed class ScannerSessionTests
{
    [Theory]
    [InlineData(PageSeparation.FullBatch, 1, 1)]
    [InlineData(PageSeparation.EveryPage, 1, 5)]
    [InlineData(PageSeparation.EveryTwoPages, 2, 3)]
    [InlineData(PageSeparation.EveryNPages, 3, 2)]
    public async Task Scan_MapsProfileAndReturnsManagedReference(PageSeparation separation, int n, int fileCount)
    {
        var engine = new FakeEngine { FileCount = fileCount };
        await using var connection = await Connection.Open(engine);
        Assert.Equal("healthy", connection.Health.Status);
        var profile = new ScanProfile { Resolution = 600, ColorMode = ScanColorMode.Grayscale,
            Duplex = true, Source = ScanSource.Adf, Separation = separation, PagesPerFile = n, DeviceId = "fake" };
        var response = await connection.Send(profile);
        Assert.Equal(profile, engine.LastProfile);
        Assert.Equal(ScannerMessageTypes.Result, response.MessageType);
        Assert.Equal("request-1", response.CorrelationId);
        var reference = response.DeserializePayload<LargeDataReference>()!;
        Assert.True(new ManagedTempPathValidator(connection.Options).IsWithinManagedTempRoot(reference.Path));
        Assert.True(File.Exists(reference.Path));
        Assert.False(response.Payload.TryGetProperty("Code", out _));
        if (fileCount > 1)
        {
            using var zip = ZipFile.OpenRead(reference.Path);
            Assert.Equal(fileCount, zip.Entries.Count);
            Assert.Equal("document-0001.pdf", zip.Entries[0].FullName);
        }
        else Assert.Equal(".pdf", Path.GetExtension(reference.Path));
    }

    [Theory]
    [InlineData("no_scanner")]
    [InlineData("device_error")]
    [InlineData("cancelled")]
    public async Task Failure_IsExplicitAndConnectionRemainsUsable(string code)
    {
        var engine = new FakeEngine { Failure = code, Available = code != "no_scanner" };
        await using var connection = await Connection.Open(engine);
        Assert.Equal(engine.Available ? "healthy" : "degraded", connection.Health.Status);
        var response = await connection.Send(new ScanProfile());
        Assert.Equal(code, response.DeserializePayload<ScanFailure>()!.Code);
        Assert.Empty(Directory.GetDirectories(connection.Options.ManagedTempStorageRoot));
        engine.Failure = null;
        engine.Available = true;
        Assert.NotNull((await connection.Send(new ScanProfile())).DeserializePayload<LargeDataReference>()!.Path);
    }

    [Theory]
    [InlineData("other", 1)]
    [InlineData("scan.request", 2)]
    public async Task UnsupportedMessage_ReturnsProtocolErrorAndContinues(string type, int version)
    {
        await using var connection = await Connection.Open(new FakeEngine());
        await connection.Host.SendAsync(WorkerMessage.Create(version, type, new { }), connection.Timeout.Token);
        var response = await connection.Host.ReceiveAsync(connection.Timeout.Token);
        Assert.Equal(ScannerMessageTypes.ProtocolError, response.MessageType);
        Assert.NotNull((await connection.Send(new ScanProfile())).DeserializePayload<LargeDataReference>()!.Path);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"Resolution\":0}")]
    [InlineData("{\"resolution\":600}")]
    [InlineData("{\"ColorMode\":\"bad\"}")]
    [InlineData("{\"Duplex\":true,\"Source\":\"Flatbed\"}")]
    [InlineData("{\"Separation\":\"EveryNPages\",\"PagesPerFile\":0}")]
    public async Task InvalidProfile_DoesNotCallEngine(string json)
    {
        var engine = new FakeEngine();
        await using var connection = await Connection.Open(engine);
        using var payload = System.Text.Json.JsonDocument.Parse(json);
        await connection.Host.SendAsync(WorkerMessage.Create(1, ScannerMessageTypes.Request, payload.RootElement), connection.Timeout.Token);
        var response = await connection.Host.ReceiveAsync(connection.Timeout.Token);
        Assert.Equal("invalid_profile", response.DeserializePayload<ScanFailure>()!.Code);
        Assert.Null(engine.LastProfile);
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("cancel")]
    [InlineData("escape")]
    [InlineData("empty")]
    public async Task Faults_DoNotReturnSuccessOrLeavePartialOutput(string mode)
    {
        await using var connection = await Connection.Open(new FakeEngine { Mode = mode });
        var response = await connection.Send(new ScanProfile());
        Assert.Equal(mode == "cancel" ? "cancelled" : mode == "empty" ? "no_pages" : "device_error",
            response.DeserializePayload<ScanFailure>()!.Code);
        Assert.Empty(Directory.GetDirectories(connection.Options.ManagedTempStorageRoot));
    }

    [Fact]
    public async Task HostDisconnect_EndsSession()
    {
        await using var connection = await Connection.Open(new FakeEngine());
        await connection.Host.DisposeAsync();
        await connection.Running.WaitAsync(connection.Timeout.Token);
    }

    private sealed class FakeEngine : IScannerEngine
    {
        public bool Available { get; set; } = true;
        public string? Failure { get; set; }
        public string? Mode { get; init; }
        public int FileCount { get; init; } = 1;
        public ScanProfile? LastProfile { get; private set; }
        public Task<IReadOnlyList<ScannerDevice>> GetDevicesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ScannerDevice>>(Available ? [new("fake", "Fake scanner")] : []);
        public async Task<ScanBatch> ScanAsync(ScanProfile profile, string outputDirectory, CancellationToken cancellationToken)
        {
            LastProfile = profile;
            var files = new List<string>();
            for (var i = 0; i < FileCount; i++)
            {
                var file = Path.Combine(outputDirectory, $"page-{i}.pdf");
                await File.WriteAllTextAsync(file, "%PDF-1.4 fake engine output", cancellationToken);
                files.Add(file);
            }
            if (Mode == "throw") throw new IOException("Device jam after one page");
            if (Mode == "cancel") throw new OperationCanceledException();
            if (Mode == "escape") return new([Path.Combine(outputDirectory, "..", "outside.pdf")]);
            if (Mode == "empty") return new([]);
            return Failure is not null ? ScanBatch.Failed(Failure, "Test failure") : new(files);
        }
        public void Dispose() { }
    }

    private sealed class Connection : IAsyncDisposable
    {
        public WorkerProtocolOptions Options { get; } = new()
        {
            ManagedTempStorageRoot = Path.Combine(Path.GetTempPath(), "Darhous.Scanner.Tests", Guid.NewGuid().ToString("N")),
        };
        public CancellationTokenSource Timeout { get; } = new(TimeSpan.FromSeconds(30));
        public NamedPipeTransport Host { get; private set; } = null!;
        private NamedPipeTransport _worker = null!;
        public Task Running { get; private set; } = null!;
        public WorkerHealthPayload Health { get; private set; } = null!;

        public static async Task<Connection> Open(FakeEngine engine)
        {
            var connection = new Connection();
            var name = NamedPipeTransport.CreateUniquePipeName(connection.Options);
            connection.Host = NamedPipeTransport.CreateServer(name, connection.Options);
            var client = NamedPipeTransport.ConnectClientAsync(name, connection.Options, connection.Timeout.Token);
            await Task.WhenAll(client, connection.Host.WaitForConnectionAsync(connection.Timeout.Token));
            connection._worker = await client;
            var handshake = new WorkerHandshakeHost(connection.Options);
            connection.Running = new ScannerWorkerSession(engine, connection.Options)
                .RunAsync(connection._worker, handshake.SessionToken, connection.Timeout.Token);
            var description = await handshake.AcceptAsync(connection.Host, ScannerWorkerSession.WorkerId, connection.Timeout.Token);
            connection.Health = description.Health;
            Assert.Equal(ScannerWorkerSession.Capabilities, description.Capabilities);
            return connection;
        }

        public async Task<WorkerMessage> Send(ScanProfile profile)
        {
            await Host.SendAsync(WorkerMessage.Create(1, ScannerMessageTypes.Request, profile, requestId: "request-1"), Timeout.Token);
            return await Host.ReceiveAsync(Timeout.Token);
        }

        public async ValueTask DisposeAsync()
        {
            await Timeout.CancelAsync();
            await Running;
            await Host.DisposeAsync();
            await _worker.DisposeAsync();
            Timeout.Dispose();
            if (Directory.Exists(Options.ManagedTempStorageRoot)) Directory.Delete(Options.ManagedTempStorageRoot, true);
        }
    }
}
