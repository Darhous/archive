using Darhous.Archive.Workers.Protocol;

namespace Darhous.Archive.Workers.Transport;

/// <summary>Frame transport over any duplex stream.</summary>
public sealed class StreamWorkerTransport : IWorkerTransport
{
    private readonly Stream _stream;
    private readonly WorkerFrameCodec _codec;
    private readonly bool _ownsStream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _receiveLock = new(1, 1);
    private bool _disposed;

    public StreamWorkerTransport(Stream stream, WorkerFrameCodec codec, bool ownsStream = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _ownsStream = ownsStream;
    }

    public async ValueTask SendAsync(
        WorkerMessage message,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _codec.WriteAsync(_stream, message, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask<WorkerMessage> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _receiveLock.WaitAsync(cancellationToken);
        try
        {
            return await _codec.ReadAsync(_stream, cancellationToken);
        }
        finally
        {
            _receiveLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsStream)
        {
            await _stream.DisposeAsync();
        }

        _sendLock.Dispose();
        _receiveLock.Dispose();
    }
}
