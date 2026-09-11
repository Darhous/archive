namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec §46 (recycle_bin_entries) — public shape.</summary>
public sealed record RecycleBinEntry(
    Guid DocumentUid,
    Guid? OriginalFolderId,
    Guid? DeletedBy,
    DateTimeOffset DeletedAt,
    string? DeleteReason,
    DateTimeOffset? ExpiresAt);
