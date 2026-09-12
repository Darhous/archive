namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec §34 (folders) — public shape.</summary>
public sealed record Folder(
    Guid Uid,
    Guid? ParentId,
    string Name,
    int SortOrder,
    string? IconKey,
    bool IsSystem,
    bool IsHidden,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record NewFolder(Guid? ParentId, string Name, int SortOrder, Guid? CreatedBy);
