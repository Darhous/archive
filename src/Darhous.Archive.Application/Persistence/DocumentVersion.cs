using Darhous.Archive.Contracts.Documents;

namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec §29 (document_versions) — public shape.</summary>
public sealed record DocumentVersion(
    Guid Uid,
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
    bool? IsSearchablePdf,
    string? OcrProvider,
    string ContentExtractionStatus,
    string? ExtractedText,
    string? SearchableFilePath,
    string? Notes);
