namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec §52 (discovery_runs) — one row per Initial Discovery / reconciliation pass; drives the "Display discovery summary" step (Implementation Plan §50).</summary>
public sealed record DiscoveryRun(
    Guid Uid,
    string RunType,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int DrivesScanned,
    int FilesSeen,
    int SupportedFiles,
    int QueuedForIndex,
    int SkippedByExclusion,
    int MissingDetected,
    int ErrorCount);
