using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Darhous.Archive.Workers.Protocol;

namespace Darhous.Archive.Workers.Tests;

public sealed class WorkerFrameCodecTests
{
    private readonly WorkerProtocolOptions _options = new()
    {
        MaximumFrameLengthBytes = 2 * 1024 * 1024,
    };

    [Fact]
    public async Task RoundTrip_PreservesEnvelopeAndStronglyTypedPayload()
    {
        var codec = new WorkerFrameCodec(_options);
        var original = WorkerMessage.Create(
            protocolVersion: 1,
            messageType: "test.echo",
            payload: new SamplePayload("alpha", 42),
            requestId: "request-1",
            correlationId: "correlation-1");
        await using var stream = new MemoryStream();

        await codec.WriteAsync(stream, original);
        stream.Position = 0;
        var result = await codec.ReadAsync(stream);

        Assert.Equal(1, result.ProtocolVersion);
        Assert.Equal("test.echo", result.MessageType);
        Assert.Equal("request-1", result.RequestId);
        Assert.Equal("correlation-1", result.CorrelationId);
        Assert.Equal(new SamplePayload("alpha", 42), result.DeserializePayload<SamplePayload>());
    }

    [Fact]
    public async Task RoundTrip_AllowsEmptyAndNullJsonPayloads()
    {
        var codec = new WorkerFrameCodec(_options);
        await using var stream = new MemoryStream();
        var empty = WorkerMessage.Create(1, "test.empty", string.Empty);
        var jsonNull = WorkerMessage.Create<object?>(1, "test.null", null);

        await codec.WriteAsync(stream, empty);
        await codec.WriteAsync(stream, jsonNull);
        stream.Position = 0;

        Assert.Equal(string.Empty, (await codec.ReadAsync(stream)).DeserializePayload<string>());
        Assert.Equal(JsonValueKind.Null, (await codec.ReadAsync(stream)).Payload.ValueKind);
    }

    [Fact]
    public async Task ReadAsync_ReassemblesLargeFrameFromPartialReads()
    {
        var codec = new WorkerFrameCodec(_options);
        var body = new string('x', 1024 * 1024);
        var message = WorkerMessage.Create(1, "test.large", new SamplePayload(body, 7));
        await using var encoded = new MemoryStream();
        await codec.WriteAsync(encoded, message);
        await using var partial = new ChunkedReadStream(encoded.ToArray(), maximumReadSize: 113);

        var result = await codec.ReadAsync(partial);

        var payload = result.DeserializePayload<SamplePayload>();
        Assert.NotNull(payload);
        Assert.Equal(body.Length, payload.Text.Length);
        Assert.Equal(body, payload.Text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task ReadAsync_RejectsTruncatedLengthPrefix(int availableBytes)
    {
        var codec = new WorkerFrameCodec(_options);
        await using var stream = new MemoryStream(new byte[availableBytes]);

        var exception = await Assert.ThrowsAsync<WorkerProtocolException>(
            async () => await codec.ReadAsync(stream));

        Assert.Contains("length prefix", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_RejectsTruncatedJsonBody()
    {
        var codec = new WorkerFrameCodec(_options);
        await using var complete = new MemoryStream();
        await codec.WriteAsync(complete, WorkerMessage.Create(1, "test.truncated", new { Value = 12 }));
        var bytes = complete.ToArray();
        await using var truncated = new MemoryStream(bytes[..^5]);

        var exception = await Assert.ThrowsAsync<WorkerProtocolException>(
            async () => await codec.ReadAsync(truncated));

        Assert.Contains("JSON body", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_RejectsCorruptJson()
    {
        var codec = new WorkerFrameCodec(_options);
        var json = Encoding.UTF8.GetBytes("{ definitely-not-json }");
        var frame = new byte[sizeof(int) + json.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)json.Length);
        json.CopyTo(frame.AsSpan(sizeof(int)));
        await using var stream = new MemoryStream(frame);

        var exception = await Assert.ThrowsAsync<WorkerProtocolException>(
            async () => await codec.ReadAsync(stream));

        Assert.Contains("invalid JSON", exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task ReadAsync_RejectsDeclaredFrameAboveConfiguredMaximumBeforeAllocatingBody()
    {
        var options = new WorkerProtocolOptions { MaximumFrameLengthBytes = 128 };
        var codec = new WorkerFrameCodec(options);
        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteUInt32BigEndian(prefix, 129);
        await using var stream = new MemoryStream(prefix);

        var exception = await Assert.ThrowsAsync<WorkerProtocolException>(
            async () => await codec.ReadAsync(stream));

        Assert.Contains("exceeds the configured maximum", exception.Message);
    }

    private sealed record SamplePayload(string Text, int Count);

    private sealed class ChunkedReadStream(byte[] buffer, int maximumReadSize) : MemoryStream(buffer)
    {
        public override int Read(byte[] destination, int offset, int count) =>
            base.Read(destination, offset, Math.Min(count, maximumReadSize));

        public override ValueTask<int> ReadAsync(
            Memory<byte> destination,
            CancellationToken cancellationToken = default) =>
            base.ReadAsync(destination[..Math.Min(destination.Length, maximumReadSize)], cancellationToken);
    }
}
