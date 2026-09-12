namespace Darhous.Archive.Application.Persistence;

public interface ISourceDriveRepository
{
    /// <summary>Insert-or-refresh-last-seen for a drive found during enumeration — idempotent.</summary>
    Task UpsertSeenAsync(NewSourceDrive drive, DateTimeOffset seenAt, CancellationToken cancellationToken);

    Task<IReadOnlyList<SourceDrive>> ListEnabledForAutoDiscoverAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SourceDrive>> ListAllAsync(CancellationToken cancellationToken);

    Task SetLastDiscoveryAtAsync(Guid uid, DateTimeOffset at, CancellationToken cancellationToken);
}
