namespace Darhous.Search.SqliteFts.Indexing;

/// <summary>Everything the indexer needs to (re)build one document's search.db row — assembled from archive.db by <see cref="SearchReconciliationService"/>.</summary>
public sealed record SearchDocumentSnapshot(
    Guid DocumentUid,
    string ArchiveNumber,
    string Title,
    string FileName,
    string? FileType,
    Guid? FolderId,
    DateOnly ArchiveDate,
    string Status,
    string? Body,
    DateTimeOffset SourceUpdatedAt);
