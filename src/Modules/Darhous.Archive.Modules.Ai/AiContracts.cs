namespace Darhous.Archive.Modules.Ai;

/// <summary>Provider-neutral model identity. Provider-specific capabilities remain provider metadata.</summary>
public sealed record AiModelInfo(
    string ModelId,
    string DisplayName,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>A deliberately small text request contract for future enrichment features.</summary>
public sealed record AiRequest(
    string ModelId,
    string Prompt,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>A provider response without any provider-specific SDK types.</summary>
public sealed record AiResponse(
    string ModelId,
    string Content,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// Result returned to document-side callers. A false result is intentionally data, not an
/// exception, so optional AI enrichment cannot roll back or fail the caller's own operation.
/// </summary>
public sealed record AiAssistResult(
    bool Enriched,
    string? Content,
    string? ProviderId,
    string? FailureCode)
{
    public static AiAssistResult Succeeded(string content, string providerId) =>
        new(true, content, providerId, null);

    public static AiAssistResult NoEnrichment(string failureCode, string? providerId = null) =>
        new(false, null, providerId, failureCode);
}
