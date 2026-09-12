using System.Buffers;
using System.Buffers.Binary;
using System.Text.Json;

namespace Darhous.Archive.Workers.Protocol;

/// <summary>
/// Reads and writes one length-prefixed UTF-8 JSON frame. The four-byte length is an
/// unsigned payload byte count in network byte order (big-endian).
/// </summary>
public sealed class WorkerFrameCodec
{
    private const int PrefixLength = sizeof(int);

    private readonly int _maximumFrameLengthBytes;
    private readonly JsonSerializerOptions _serializerOptions;

    public WorkerFrameCodec(
        WorkerProtocolOptions options,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaximumFrameLengthBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaximumFrameLengthBytes,
                "Maximum frame length must be positive.");
        }

        _maximumFrameLengthBytes = options.MaximumFrameLengthBytes;
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions();
    }

    public async ValueTask WriteAsync(
        Stream stream,
        WorkerMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);

        ValidateEnvelope(message);
        var json = JsonSerializer.SerializeToUtf8Bytes(message, _serializerOptions);
        if (json.Length > _maximumFrameLengthBytes)
        {
            throw new WorkerProtocolException(
                $"Worker frame length {json.Length} exceeds the configured maximum of {_maximumFrameLengthBytes} bytes.");
        }

        var prefix = new byte[PrefixLength];
        BinaryPrimitives.WriteUInt32BigEndian(prefix, checked((uint)json.Length));

        await stream.WriteAsync(prefix, cancellationToken);
        await stream.WriteAsync(json, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public async ValueTask<WorkerMessage> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var prefix = new byte[PrefixLength];
        await ReadExactlyAsync(stream, prefix, "four-byte frame length prefix", cancellationToken);

        var frameLength = BinaryPrimitives.ReadUInt32BigEndian(prefix);
        if (frameLength == 0)
        {
            throw new WorkerProtocolException("Worker frame declared an empty JSON body.");
        }

        if (frameLength > _maximumFrameLengthBytes)
        {
            throw new WorkerProtocolException(
                $"Worker frame length {frameLength} exceeds the configured maximum of {_maximumFrameLengthBytes} bytes.");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(checked((int)frameLength));
        try
        {
            await ReadExactlyAsync(
                stream,
                buffer.AsMemory(0, (int)frameLength),
                $"{frameLength}-byte JSON body",
                cancellationToken);

            WorkerMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<WorkerMessage>(
                    buffer.AsSpan(0, (int)frameLength),
                    _serializerOptions);
            }
            catch (JsonException ex)
            {
                throw new WorkerProtocolException("Worker frame contains invalid JSON.", ex);
            }

            if (message is null)
            {
                throw new WorkerProtocolException("Worker frame JSON was null instead of a message envelope.");
            }

            ValidateEnvelope(message);
            return message;
        }
        finally
        {
            // Frames can contain the per-launch session token. Do not leave protocol data
            // available to an unrelated future renter of the shared buffer.
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static async ValueTask ReadExactlyAsync(
        Stream stream,
        Memory<byte> destination,
        string framePart,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var bytesRead = await stream.ReadAsync(destination[offset..], cancellationToken);
            if (bytesRead == 0)
            {
                throw new WorkerProtocolException(
                    $"Worker stream ended after {offset} of {destination.Length} bytes while reading the {framePart}.");
            }

            offset += bytesRead;
        }
    }

    private static void ValidateEnvelope(WorkerMessage message)
    {
        if (message.ProtocolVersion <= 0)
        {
            throw new WorkerProtocolException("Worker message protocolVersion must be positive.");
        }

        if (string.IsNullOrWhiteSpace(message.MessageType))
        {
            throw new WorkerProtocolException("Worker message messageType is required.");
        }

        if (string.IsNullOrWhiteSpace(message.RequestId))
        {
            throw new WorkerProtocolException("Worker message requestId is required.");
        }

        if (message.Payload.ValueKind == JsonValueKind.Undefined)
        {
            throw new WorkerProtocolException("Worker message payload is required (JSON null is allowed).");
        }
    }
}
