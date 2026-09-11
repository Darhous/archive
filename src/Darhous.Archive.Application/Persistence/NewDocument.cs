using Darhous.Archive.Contracts.Documents;

namespace Darhous.Archive.Application.Persistence;

public sealed record NewDocument(
    string ArchiveNumber,
    string Title,
    Guid? FolderId,
    DocumentSourceType SourceType,
    DocumentStorageMode StorageMode,
    DateOnly? DocumentDate,
    DateOnly? ScanDate,
    DateOnly ArchiveDate,
    Guid? CreatedBy);

public sealed record NewDocumentVersion(
    Guid DocumentUid,
    int VersionNo,
    string OriginalFileName,
    string StoredFileName,
    string FilePath,
    string FileExtension,
    string? MimeType,
    long FileSize,
    string Sha256,
    int? PageCount,
    DateTimeOffset? FileCreatedAt,
    DateTimeOffset? FileModifiedAt,
    DateTimeOffset ImportedAt,
    Guid? CreatedBy,
    DocumentAvailabilityStatus AvailabilityStatus,
    string ContentExtractionStatus,
    string? OcrProvider = null);
