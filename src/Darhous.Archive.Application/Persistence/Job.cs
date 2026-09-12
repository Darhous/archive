using Darhous.Archive.Contracts.Jobs;

namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec §jobs (built ahead of need in Phase 2; wired up for real starting Phase 9's Discovery jobs) — public shape.</summary>
public sealed record Job(
    Guid Uid,
    string JobType,
    string OwnerComponent,
    string? PluginId,
    Guid? UserId,
    JobStatus Status,
    int ProgressPercent,
    string? PayloadJson,
    string? ResultJson,
    string? ErrorCode,
    string? ErrorMessage,
    int RetryCount,
    int MaxRetries,
    int Priority,
    Guid? CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? NextRetryAt);

public sealed record NewJob(
    string JobType,
    string OwnerComponent,
    string? PluginId,
    Guid? UserId,
    string? PayloadJson,
    int MaxRetries,
    int Priority,
    Guid? CorrelationId);
