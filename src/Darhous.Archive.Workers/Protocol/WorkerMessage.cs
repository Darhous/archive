using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darhous.Archive.Workers.Protocol;

/// <summary>The common envelope for every worker IPC message (SAD §64).</summary>
public sealed record WorkerMessage
{
    [JsonPropertyName("protocolVersion")]
    public required int ProtocolVersion { get; init; }

    [JsonPropertyName("messageType")]
    public required string MessageType { get; init; }

    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("payload")]
    public required JsonElement Payload { get; init; }

    public static WorkerMessage Create<TPayload>(
        int protocolVersion,
        string messageType,
        TPayload payload,
        string? requestId = null,
        string? correlationId = null,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);

        return new WorkerMessage
        {
            ProtocolVersion = protocolVersion,
            MessageType = messageType,
            RequestId = requestId ?? Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            Payload = JsonSerializer.SerializeToElement(payload, serializerOptions),
        };
    }

    public TPayload? DeserializePayload<TPayload>(JsonSerializerOptions? serializerOptions = null) =>
        Payload.Deserialize<TPayload>(serializerOptions);
}
