namespace Darhous.Archive.Application.Persistence;

public interface IDiscoveryRunRepository
{
    Task<Guid> StartAsync(string runType, Guid? requestedBy, DateTimeOffset startedAt, CancellationToken cancellationToken);

    Task<DiscoveryRun?> GetByUidAsync(Guid uid, CancellationToken cancellationToken);

    Task IncrementCountersAsync(
        Guid uid, int filesSeenDelta, int supportedFilesDelta, int queuedForIndexDelta,
        int skippedByExclusionDelta, int missingDetectedDelta, int errorCountDelta, CancellationToken cancellationToken);

    Task CompleteAsync(Guid uid, string status, int drivesScanned, DateTimeOffset completedAt, CancellationToken cancellationToken);
}
