using System.Security.Cryptography;
using System.Text;
using Darhous.Archive.Workers.Transport;

namespace Darhous.Archive.Workers.Protocol;

public static class WorkerMessageTypes
{
    public const string HandshakeProtocolVersion = "handshake.protocol-version";
    public const string HandshakeWorkerId = "handshake.worker-id";
    public const string HandshakeWorkerVersion = "handshake.worker-version";
    public const string HandshakeSessionToken = "handshake.session-token";
    public const string HandshakeCapabilities = "handshake.capabilities";
    public const string HandshakeHealth = "handshake.health";
    public const string HandshakeReady = "handshake.ready";
    public const string LargeDataReference = "data.path-reference";
}

public enum WorkerHandshakeState
{
    NotStarted,
    ProtocolVersion,
    WorkerId,
    WorkerVersion,
    SessionToken,
    Capabilities,
    Health,
    Ready,
    Completed,
    Rejected,
}

public sealed record WorkerHandshakeDescription(
    string WorkerId,
    string WorkerVersion,
    IReadOnlyList<string> Capabilities,
    WorkerHealthPayload Health);

public sealed record WorkerHandshakeResult(
    string WorkerId,
    string WorkerVersion,
    IReadOnlyList<string> Capabilities,
    WorkerHealthPayload Health);

public sealed record ProtocolVersionPayload(int ProtocolVersion);

public sealed record WorkerIdPayload(string WorkerId);

public sealed record WorkerVersionPayload(string WorkerVersion);

public sealed record SessionTokenPayload(string SessionToken);

public sealed record WorkerCapabilitiesPayload(IReadOnlyList<string> Capabilities);

public sealed record WorkerHealthPayload(string Status, string? Detail = null);

public sealed record WorkerReadyPayload(bool IsReady);

public static class SessionTokenGenerator
{
    private const int TokenLengthBytes = 32;

    public static string Create() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenLengthBytes));
}

/// <summary>Sends the worker side of the ordered SAD §67 handshake.</summary>
public sealed class WorkerHandshakeClient(WorkerProtocolOptions options)
{
    public WorkerHandshakeState State { get; private set; } = WorkerHandshakeState.NotStarted;

    public async Task PerformAsync(
        IWorkerTransport transport,
        WorkerHandshakeDescription description,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(description.WorkerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(description.WorkerVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        if (State != WorkerHandshakeState.NotStarted)
        {
            throw new InvalidOperationException("A worker handshake client instance can perform only one handshake.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.HandshakeTimeout);

        try
        {
            await SendAsync(
                transport,
                WorkerMessageTypes.HandshakeProtocolVersion,
                new ProtocolVersionPayload(options.ProtocolVersion),
                WorkerHandshakeState.ProtocolVersion,
                timeout.Token);
            await SendAsync(
                transport,
                WorkerMessageTypes.HandshakeWorkerId,
                new WorkerIdPayload(description.WorkerId),
                WorkerHandshakeState.WorkerId,
                timeout.Token);
            await SendAsync(
                transport,
                WorkerMessageTypes.HandshakeWorkerVersion,
                new WorkerVersionPayload(description.WorkerVersion),
                WorkerHandshakeState.WorkerVersion,
                timeout.Token);
            await SendAsync(
                transport,
                WorkerMessageTypes.HandshakeSessionToken,
                new SessionTokenPayload(sessionToken),
                WorkerHandshakeState.SessionToken,
                timeout.Token);
            await SendAsync(
                transport,
                WorkerMessageTypes.HandshakeCapabilities,
                new WorkerCapabilitiesPayload(description.Capabilities),
                WorkerHandshakeState.Capabilities,
                timeout.Token);
            await SendAsync(
                transport,
                WorkerMessageTypes.HandshakeHealth,
                description.Health,
                WorkerHandshakeState.Health,
                timeout.Token);
            await SendAsync(
                transport,
                WorkerMessageTypes.HandshakeReady,
                new WorkerReadyPayload(true),
                WorkerHandshakeState.Ready,
                timeout.Token);

            State = WorkerHandshakeState.Completed;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            State = WorkerHandshakeState.Rejected;
            throw new WorkerHandshakeException(
                $"Worker handshake timed out after {options.HandshakeTimeout}.",
                ex);
        }
        catch
        {
            State = WorkerHandshakeState.Rejected;
            throw;
        }
    }

    private async Task SendAsync<TPayload>(
        IWorkerTransport transport,
        string messageType,
        TPayload payload,
        WorkerHandshakeState state,
        CancellationToken cancellationToken)
    {
        State = state;
        await transport.SendAsync(
            WorkerMessage.Create(options.ProtocolVersion, messageType, payload),
            cancellationToken);
    }
}

/// <summary>Receives and validates the host side of the ordered SAD §67 handshake.</summary>
public sealed class WorkerHandshakeHost
{
    private readonly WorkerProtocolOptions _options;

    public WorkerHandshakeHost(WorkerProtocolOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        SessionToken = SessionTokenGenerator.Create();
    }

    /// <summary>
    /// A fresh cryptographically random token for this host/worker launch. The process
    /// launcher must convey it through its secure startup channel; that channel is outside
    /// this protocol-only component.
    /// </summary>
    public string SessionToken { get; }

    public WorkerHandshakeState State { get; private set; } = WorkerHandshakeState.NotStarted;

    public async Task<WorkerHandshakeResult> AcceptAsync(
        IWorkerTransport transport,
        string? expectedWorkerId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);

        if (State != WorkerHandshakeState.NotStarted)
        {
            throw new InvalidOperationException("A worker handshake host instance can accept only one handshake.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.HandshakeTimeout);

        try
        {
            var version = await ReceiveAsync<ProtocolVersionPayload>(
                transport,
                WorkerMessageTypes.HandshakeProtocolVersion,
                WorkerHandshakeState.ProtocolVersion,
                timeout.Token);
            if (version.ProtocolVersion != _options.ProtocolVersion)
            {
                Reject($"Worker protocol version {version.ProtocolVersion} does not match host version {_options.ProtocolVersion}.");
            }

            var workerId = await ReceiveAsync<WorkerIdPayload>(
                transport,
                WorkerMessageTypes.HandshakeWorkerId,
                WorkerHandshakeState.WorkerId,
                timeout.Token);
            if (string.IsNullOrWhiteSpace(workerId.WorkerId))
            {
                Reject("Worker handshake supplied an empty worker ID.");
            }

            if (expectedWorkerId is not null &&
                !string.Equals(workerId.WorkerId, expectedWorkerId, StringComparison.Ordinal))
            {
                Reject($"Worker ID '{workerId.WorkerId}' does not match expected ID '{expectedWorkerId}'.");
            }

            var workerVersion = await ReceiveAsync<WorkerVersionPayload>(
                transport,
                WorkerMessageTypes.HandshakeWorkerVersion,
                WorkerHandshakeState.WorkerVersion,
                timeout.Token);
            if (string.IsNullOrWhiteSpace(workerVersion.WorkerVersion))
            {
                Reject("Worker handshake supplied an empty worker version.");
            }

            var token = await ReceiveAsync<SessionTokenPayload>(
                transport,
                WorkerMessageTypes.HandshakeSessionToken,
                WorkerHandshakeState.SessionToken,
                timeout.Token);
            if (!TokensMatch(SessionToken, token.SessionToken))
            {
                Reject("Worker handshake session token was rejected.");
            }

            var capabilities = await ReceiveAsync<WorkerCapabilitiesPayload>(
                transport,
                WorkerMessageTypes.HandshakeCapabilities,
                WorkerHandshakeState.Capabilities,
                timeout.Token);
            var capabilityList = capabilities.Capabilities
                ?? throw new WorkerHandshakeException("Worker handshake capabilities payload was null.");

            var health = await ReceiveAsync<WorkerHealthPayload>(
                transport,
                WorkerMessageTypes.HandshakeHealth,
                WorkerHandshakeState.Health,
                timeout.Token);
            if (string.IsNullOrWhiteSpace(health.Status))
            {
                Reject("Worker handshake supplied an empty health status.");
            }

            var ready = await ReceiveAsync<WorkerReadyPayload>(
                transport,
                WorkerMessageTypes.HandshakeReady,
                WorkerHandshakeState.Ready,
                timeout.Token);
            if (!ready.IsReady)
            {
                Reject("Worker handshake did not declare the worker ready.");
            }

            State = WorkerHandshakeState.Completed;
            return new WorkerHandshakeResult(
                workerId.WorkerId,
                workerVersion.WorkerVersion,
                capabilityList.ToArray(),
                health);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            State = WorkerHandshakeState.Rejected;
            throw new WorkerHandshakeException(
                $"Worker handshake timed out after {_options.HandshakeTimeout}.",
                ex);
        }
        catch
        {
            State = WorkerHandshakeState.Rejected;
            throw;
        }
    }

    private async Task<TPayload> ReceiveAsync<TPayload>(
        IWorkerTransport transport,
        string expectedMessageType,
        WorkerHandshakeState state,
        CancellationToken cancellationToken)
    {
        State = state;
        var message = await transport.ReceiveAsync(cancellationToken);
        if (message.ProtocolVersion != _options.ProtocolVersion)
        {
            Reject(
                $"Worker message '{message.MessageType}' uses protocol version {message.ProtocolVersion}; " +
                $"host requires {_options.ProtocolVersion}.");
        }

        if (!string.Equals(message.MessageType, expectedMessageType, StringComparison.Ordinal))
        {
            Reject(
                $"Worker handshake expected '{expectedMessageType}' at state {state}, " +
                $"but received '{message.MessageType}'.");
        }

        TPayload? payload;
        try
        {
            payload = message.DeserializePayload<TPayload>();
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            throw new WorkerHandshakeException(
                $"Worker handshake message '{expectedMessageType}' has an invalid payload.",
                ex);
        }

        return payload
            ?? throw new WorkerHandshakeException(
                $"Worker handshake message '{expectedMessageType}' has a null payload.");
    }

    private static bool TokensMatch(string expected, string actual)
    {
        if (actual is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual));
    }

    private void Reject(string message)
    {
        State = WorkerHandshakeState.Rejected;
        throw new WorkerHandshakeException(message);
    }
}
