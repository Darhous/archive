using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Darhous.Archive.Workers.Protocol;

namespace Darhous.Archive.Workers.Transport;

/// <summary>A Windows named-pipe endpoint secured to the current user SID.</summary>
[SupportedOSPlatform("windows")]
public sealed class NamedPipeTransport : IWorkerTransport
{
    private readonly PipeStream _pipe;
    private readonly StreamWorkerTransport _transport;

    private NamedPipeTransport(PipeStream pipe, WorkerFrameCodec codec)
    {
        _pipe = pipe;
        _transport = new StreamWorkerTransport(pipe, codec, ownsStream: true);
    }

    public bool IsConnected => _pipe.IsConnected;

    public static string CreateUniquePipeName(WorkerProtocolOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PipeNamePrefix);
        return $"{options.PipeNamePrefix}.{Guid.NewGuid():N}";
    }

    public static NamedPipeTransport CreateServer(string pipeName, WorkerProtocolOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(options);

        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
        var userSid = identity.User
            ?? throw new WorkerProtocolException("The current Windows identity does not have a user SID.");

        var security = new PipeSecurity();
        security.SetOwner(userSid);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(
            new PipeAccessRule(
                userSid,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

        var pipe = NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            security,
            HandleInheritability.None,
            (PipeAccessRights)0);

        return new NamedPipeTransport(pipe, new WorkerFrameCodec(options));
    }

    public static async Task<NamedPipeTransport> ConnectClientAsync(
        string pipeName,
        WorkerProtocolOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(options);

        var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Identification);

        try
        {
            await pipe.ConnectAsync(cancellationToken);
            return new NamedPipeTransport(pipe, new WorkerFrameCodec(options));
        }
        catch
        {
            await pipe.DisposeAsync();
            throw;
        }
    }

    public Task WaitForConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_pipe is not NamedPipeServerStream server)
        {
            throw new InvalidOperationException("Only a named-pipe server can wait for a connection.");
        }

        return server.WaitForConnectionAsync(cancellationToken);
    }

    public ValueTask SendAsync(WorkerMessage message, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(message, cancellationToken);

    public ValueTask<WorkerMessage> ReceiveAsync(CancellationToken cancellationToken = default) =>
        _transport.ReceiveAsync(cancellationToken);

    public ValueTask DisposeAsync() => _transport.DisposeAsync();
}
