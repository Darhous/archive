namespace Darhous.Search.SqliteFts.Contracts;

/// <summary>
/// One search result row. <paramref name="Title"/> and every other display field come from
/// <c>search_documents</c> (a shadow copy synced from archive.db) — DB Spec §93: "الـDisplay
/// title والMetadata النهائية تؤخذ من archive.db"; search.db only decides ranking/matching.
/// </summary>
public sealed record SearchHit(
    Guid DocumentUid,
    string ArchiveNumber,
    string Title,
    string? Snippet,
    Guid? FolderId,
    DateOnly ArchiveDate,
    string Status,
    double Rank);
