using Darhous.Archive.Contracts.Documents;

namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec §23 (documents) — public shape; the SQLite rowid never leaves Persistence.</summary>
public sealed record Document(
    Guid Uid,
    string ArchiveNumber,
    string Title,
    Guid? FolderId,
    Guid? CurrentVersionId,
    DocumentStatus Status,
    DocumentSourceType SourceType,
    DocumentStorageMode StorageMode,
    DateOnly? DocumentDate,
    DateOnly? ScanDate,
    DateOnly ArchiveDate,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? UpdatedBy,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt);
