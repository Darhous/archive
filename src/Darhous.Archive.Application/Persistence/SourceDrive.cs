namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec §50 (source_drives). `DriveType` is one of "fixed"/"removable"/"network"/"unknown". Default policy (SAD §46.9/DB Spec §50): fixed drives get `AutoDiscover=true`, everything else starts false until a user opts in.</summary>
public sealed record SourceDrive(
    Guid Uid,
    string DriveRoot,
    string? VolumeLabel,
    string DriveType,
    bool IsEnabled,
    bool AutoDiscover,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? LastDiscoveryAt);

public sealed record NewSourceDrive(string DriveRoot, string? VolumeLabel, string DriveType, bool AutoDiscover);
