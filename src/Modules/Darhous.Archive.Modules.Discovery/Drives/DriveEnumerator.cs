using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Time;

namespace Darhous.Archive.Modules.Discovery.Drives;

/// <summary>
/// DB Spec §50 policy: local fixed drives get <c>auto_discover=true</c> by default; removable
/// and network drives start disabled until a user opts in explicitly (never auto-scanned).
/// </summary>
public sealed class DriveEnumerator(IUnitOfWork unitOfWork, IClock clock) : IDriveEnumerator
{
    public async Task<IReadOnlyList<SourceDrive>> RefreshAndListAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
            {
                continue;
            }

            var (type, autoDiscover) = Classify(drive.DriveType);

            await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
            {
                await context.SourceDrives.UpsertSeenAsync(
                    new NewSourceDrive(drive.RootDirectory.FullName, SafeVolumeLabel(drive), type, autoDiscover), now, ct);
                return null;
            }, cancellationToken);
        }

        return await unitOfWork.ExecuteAsync((context, ct) => context.SourceDrives.ListAllAsync(ct), cancellationToken);
    }

    private static (string Type, bool AutoDiscover) Classify(DriveType driveType) => driveType switch
    {
        DriveType.Fixed => ("fixed", true),
        DriveType.Removable => ("removable", false),
        DriveType.Network => ("network", false),
        _ => ("unknown", false),
    };

    private static string? SafeVolumeLabel(DriveInfo drive)
    {
        try
        {
            return drive.VolumeLabel;
        }
        catch (IOException)
        {
            // A removable drive can report IsReady=true and still fail on VolumeLabel in a race
            // (media ejected between the two calls) — the drive root itself is what matters.
            return null;
        }
    }
}
