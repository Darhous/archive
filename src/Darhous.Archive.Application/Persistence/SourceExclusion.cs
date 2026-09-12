namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec §51 (source_exclusions). `exclusion_type` is one of "drive"/"folder"/"subfolder"; `PathNormalized` is what <c>DiscoveryScanner</c> actually prefix-matches against.</summary>
public sealed record SourceExclusion(
    Guid Uid,
    string ExclusionType,
    string Path,
    string PathNormalized,
    bool IsSystem,
    bool IsEnabled,
    string? Reason,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record NewSourceExclusion(string ExclusionType, string Path, bool IsSystem, string? Reason, Guid? CreatedBy);
