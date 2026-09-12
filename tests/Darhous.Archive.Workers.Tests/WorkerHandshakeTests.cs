using System.Runtime.Versioning;
using Darhous.Archive.Workers.Protocol;
using Darhous.Archive.Workers.Transport;

namespace Darhous.Archive.Workers.Tests;

[SupportedOSPlatform("windows")]
public sealed class WorkerHandshakeTests
{
    [Fact]
    public async Task Handshake_AcceptsCorrectTokenAndReturnsWorkerMetadata()
    {
        var options = CreateOptions();
        await using var pair = await ConnectedPipePair.CreateAsync(options);
        var host = new WorkerHandshakeHost(options);
        var worker = new WorkerHandshakeClient(options);
        var description = new WorkerHandshakeDescription(
            "plugin.ocr",
            "2.4.1",
            ["ocr", "arabic"],
            new WorkerHealthPayload("healthy"));

        var hostTask = host.AcceptAsync(pair.Server, expectedWorkerId: "plugin.ocr");
        var workerTask = worker.PerformAsync(pair.Client, description, host.SessionToken);
        var result = await hostTask;
        await workerTask;

        Assert.Equal(WorkerHandshakeState.Completed, host.State);
        Assert.Equal(WorkerHandshakeState.Completed, worker.State);
        Assert.Equal("plugin.ocr", result.WorkerId);
        Assert.Equal("2.4.1", result.WorkerVersion);
        Assert.Equal(["ocr", "arabic"], result.Capabilities);
        Assert.Equal("healthy", result.Health.Status);
    }

    [Fact]
    public async Task Handshake_RejectsWrongSessionTokenWithClearError()
    {
        var options = CreateOptions();
        await using var pair = await ConnectedPipePair.CreateAsync(options);
        var host = new WorkerHandshakeHost(options);
        var worker = new WorkerHandshakeClient(options);
        var description = new WorkerHandshakeDescription(
            "plugin.ocr",
            "2.4.1",
            [],
            new WorkerHealthPayload("healthy"));
        using var workerCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var hostTask = host.AcceptAsync(pair.Server);
        var workerTask = worker.PerformAsync(
            pair.Client,
            description,
            SessionTokenGenerator.Create(),
            workerCancellation.Token);
        var exception = await Assert.ThrowsAsync<WorkerHandshakeException>(async () => await hostTask);
        await workerCancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await workerTask);

        Assert.Equal(WorkerHandshakeState.Rejected, host.State);
        Assert.Equal(WorkerHandshakeState.Rejected, worker.State);
        Assert.Contains("session token was rejected", exception.Message);
    }

    [Fact]
    public async Task Handshake_RejectsMessageSentOutOfOrder()
    {
        var options = CreateOptions();
        await using var pair = await ConnectedPipePair.CreateAsync(options);
        var host = new WorkerHandshakeHost(options);
        var hostTask = host.AcceptAsync(pair.Server);

        await pair.Client.SendAsync(
            WorkerMessage.Create(
                options.ProtocolVersion,
                WorkerMessageTypes.HandshakeWorkerId,
                new WorkerIdPayload("too-early")));
        var exception = await Assert.ThrowsAsync<WorkerHandshakeException>(async () => await hostTask);

        Assert.Equal(WorkerHandshakeState.Rejected, host.State);
        Assert.Contains(WorkerMessageTypes.HandshakeProtocolVersion, exception.Message);
        Assert.Contains(WorkerMessageTypes.HandshakeWorkerId, exception.Message);
    }

    [Fact]
    public void Host_GeneratesIndependentCryptographicSessionTokens()
    {
        var first = new WorkerHandshakeHost(CreateOptions()).SessionToken;
        var second = new WorkerHandshakeHost(CreateOptions()).SessionToken;

        Assert.NotEqual(first, second);
        Assert.Equal(32, Convert.FromBase64String(first).Length);
        Assert.Equal(32, Convert.FromBase64String(second).Length);
    }

    private static WorkerProtocolOptions CreateOptions() => new()
    {
        PipeNamePrefix = $"Darhous.Tests.{Guid.NewGuid():N}",
        ProtocolVersion = 1,
        HandshakeTimeout = TimeSpan.FromSeconds(5),
        ManagedTempStorageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
    };

    private sealed class ConnectedPipePair : IAsyncDisposable
    {
        private ConnectedPipePair(NamedPipeTransport server, NamedPipeTransport client)
        {
            Server = server;
            Client = client;
        }

        public NamedPipeTransport Server { get; }

        public NamedPipeTransport Client { get; }

        public static async Task<ConnectedPipePair> CreateAsync(WorkerProtocolOptions options)
        {
            var name = NamedPipeTransport.CreateUniquePipeName(options);
            var server = NamedPipeTransport.CreateServer(name, options);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                var clientTask = NamedPipeTransport.ConnectClientAsync(name, options, timeout.Token);
                await Task.WhenAll(server.WaitForConnectionAsync(timeout.Token), clientTask);
                return new ConnectedPipePair(server, await clientTask);
            }
            catch
            {
                await server.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await Server.DisposeAsync();
        }
    }
}
