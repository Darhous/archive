namespace Darhous.Archive.Contracts.Jobs;

/// <summary>
/// Plugin SDK §60 (Job Metadata) — the wire/persisted shape of a background job.
/// </summary>
public sealed record JobMetadata(
    Guid JobId,
    string? PluginId,
    string JobType,
    JobStatus Status,
    int Progress,
    Guid? UserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int RetryCount,
    string? ErrorCode,
    string? ErrorMessage,
    Guid? CorrelationId,
    string? PayloadJson = null);
