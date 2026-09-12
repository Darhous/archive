using Darhous.Archive.Application.Persistence;

namespace Darhous.Archive.Modules.Discovery.Drives;

public interface IDriveEnumerator
{
    /// <summary>Enumerates currently-ready drives, upserting each into <c>source_drives</c> (Implementation Plan §46) and returning the up-to-date snapshot.</summary>
    Task<IReadOnlyList<SourceDrive>> RefreshAndListAsync(CancellationToken cancellationToken);
}
