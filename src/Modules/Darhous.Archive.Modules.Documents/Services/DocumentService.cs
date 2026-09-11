using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Audit;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Core.Audit;
using Darhous.Archive.Core.Permissions;
using Darhous.Archive.Core.Results;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Documents.Storage;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Documents.Services;

public sealed class DocumentService(
    IUnitOfWork unitOfWork, IFileStorageService fileStorage, IClock clock, IAuditService auditService,
    ILogger<DocumentService> logger)
    : IDocumentService
{
    public async Task<Result<Guid>> AddDocumentAsync(
        string sourcePath, string title, Guid? folderId, DocumentSourceType sourceType,
        DocumentStorageMode storageMode, Guid? createdBy, bool allowDuplicate, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath))
        {
            return Result<Guid>.Failure(Error.Of("DOCUMENT_SOURCE_NOT_FOUND", "الملف المصدر غير موجود."));
        }

        var documentUid = Guid.CreateVersion7();
        var fileExtension = Path.GetExtension(sourcePath);
        var originalFileName = Path.GetFileName(sourcePath);
        var now = clock.UtcNow;

        StagedFile? staged = null;
        string finalFilePath;
        string sha256;
        long fileSize;

        try
        {
            if (storageMode == DocumentStorageMode.Managed)
            {
                staged = await fileStorage.StageAsync(sourcePath, documentUid, versionNo: 1, fileExtension, cancellationToken);
                sha256 = await fileStorage.ComputeSha256Async(staged.TempPath, cancellationToken);
                fileSize = new FileInfo(staged.TempPath).Length;
                finalFilePath = staged.FinalPath;
            }
            else
            {
                sha256 = await fileStorage.ComputeSha256Async(sourcePath, cancellationToken);
                fileSize = new FileInfo(sourcePath).Length;
                finalFilePath = sourcePath;
            }

            if (!allowDuplicate)
            {
                var duplicateCheck = await CheckForDuplicateAsync(sha256, cancellationToken);
                if (duplicateCheck is not null)
                {
                    if (staged is not null)
                    {
                        await fileStorage.RollbackStagingAsync(staged, cancellationToken);
                    }

                    return Result<Guid>.Failure(Error.Of(
                        "DOCUMENT_DUPLICATE_DETECTED",
                        $"يوجد مستند مطابق بنفس المحتوى بالفعل (رقم الأرشيف: {duplicateCheck}). " +
                        "أعد المحاولة مع تفعيل allowDuplicate لو تريد الاحتفاظ بالاثنين (SAD §32)."));
                }
            }

            if (staged is not null)
            {
                await fileStorage.CommitAsync(staged, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (staged is not null)
            {
                await fileStorage.RollbackStagingAsync(staged, cancellationToken);
            }

            return Result<Guid>.Failure(Error.Of("DOCUMENT_STAGING_FAILED", ex.Message, isTransient: true));
        }

        var year = now.Year;
        Guid createdDocumentUid;

        try
        {
            createdDocumentUid = await unitOfWork.ExecuteAsync(async (context, ct) =>
            {
                var archiveNumber = await context.ArchiveNumbers.GenerateAsync(year, ct);

                // documentUid was decided before staging (it's baked into the managed file's
                // physical path — see StageAsync above), so the row must use that exact value,
                // not a repository-generated one.
                await context.Documents.CreateAsync(
                    documentUid,
                    new NewDocument(
                        archiveNumber, title, folderId, sourceType, storageMode,
                        DocumentDate: null, ScanDate: null, ArchiveDate: DateOnly.FromDateTime(now.UtcDateTime), createdBy),
                    ct);

                var versionUid = await context.DocumentVersions.CreateAsync(
                    new NewDocumentVersion(
                        documentUid, VersionNo: 1, originalFileName, Path.GetFileName(finalFilePath), finalFilePath,
                        fileExtension, MimeType: null, fileSize, sha256, PageCount: null,
                        FileCreatedAt: null, FileModifiedAt: null, now, createdBy,
                        DocumentAvailabilityStatus.Available, ContentExtractionStatus: "pending"),
                    ct);

                await context.Documents.SetCurrentVersionAsync(documentUid, versionUid, ct);
                return documentUid;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // The file is already at its final location (or unchanged, for indexed-in-place) —
            // a DB failure here orphans a file rather than corrupting app state (Phase 5 design
            // review). Logged loudly so it's visible; cleanup is a future integrity-check job.
            logger.LogError(ex, "Document metadata commit failed after file was already at {Path}", finalFilePath);
            throw;
        }

        await auditService.RecordAsync(
            new AuditEntry(
                AuditAction.AddDocument, AuditActionCategory.Document, AuditResult.Success,
                UserId: createdBy, EntityType: "document", EntityUid: createdDocumentUid.ToString(),
                EntityNameSnapshot: title),
            cancellationToken);

        return Result<Guid>.Success(createdDocumentUid);
    }

    public async Task<Result> MoveDocumentAsync(Guid documentUid, Guid? folderId, Guid? movedBy, CancellationToken cancellationToken)
    {
        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Documents.MoveToFolderAsync(documentUid, folderId, ct);
            return null;
        }, cancellationToken);

        await auditService.RecordAsync(
            new AuditEntry(
                AuditAction.MoveDocument, AuditActionCategory.Document, AuditResult.Success,
                UserId: movedBy, EntityType: "document", EntityUid: documentUid.ToString()),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> TrashDocumentAsync(Guid documentUid, Guid? deletedBy, string? reason, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            var document = await context.Documents.GetByUidAsync(documentUid, ct);
            await context.Documents.SoftDeleteAsync(documentUid, now, ct);
            await context.RecycleBin.AddAsync(
                new RecycleBinEntry(documentUid, document?.FolderId, deletedBy, now, reason, ExpiresAt: null), ct);
            return null;
        }, cancellationToken);

        await auditService.RecordAsync(
            new AuditEntry(
                AuditAction.DeleteDocument, AuditActionCategory.Document, AuditResult.Success,
                UserId: deletedBy, EntityType: "document", EntityUid: documentUid.ToString(), Details: reason),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RestoreDocumentAsync(Guid documentUid, Guid? restoredBy, CancellationToken cancellationToken)
    {
        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Documents.RestoreAsync(documentUid, ct);
            await context.RecycleBin.RemoveAsync(documentUid, ct);
            return null;
        }, cancellationToken);

        await auditService.RecordAsync(
            new AuditEntry(
                AuditAction.RestoreDocument, AuditActionCategory.Document, AuditResult.Success,
                UserId: restoredBy, EntityType: "document", EntityUid: documentUid.ToString()),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> PermanentDeleteDocumentAsync(
        Guid documentUid, UserRole requestedByRole, Guid? requestedBy, CancellationToken cancellationToken)
    {
        if (requestedByRole != UserRole.Admin)
        {
            return Result.Failure(Error.Of("DOCUMENT_PERMANENT_DELETE_FORBIDDEN", "الحذف النهائي متاح للـAdmin فقط (SAD §47)."));
        }

        var document = await unitOfWork.ExecuteAsync((context, ct) => context.Documents.GetByUidAsync(documentUid, ct), cancellationToken);
        if (document is null)
        {
            return Result.Failure(Error.Of("DOCUMENT_NOT_FOUND", "المستند غير موجود."));
        }

        var versions = await unitOfWork.ExecuteAsync(
            (context, ct) => context.DocumentVersions.ListForDocumentAsync(documentUid, ct), cancellationToken);

        // DB Spec §109 (File Deletion Atomicity): move managed files out of their live path
        // BEFORE the metadata delete commits, so a crash mid-way never leaves a live document
        // pointing at a half-deleted file — worst case is a staged file nobody points to yet.
        var stagedForDeletion = new List<string>();
        if (document.StorageMode == DocumentStorageMode.Managed)
        {
            foreach (var version in versions)
            {
                if (fileStorage.Exists(version.FilePath))
                {
                    stagedForDeletion.Add(await fileStorage.MoveToRecycleStagingAsync(version.FilePath, cancellationToken));
                }
            }
        }

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Documents.PermanentDeleteAsync(documentUid, ct);
            return null;
        }, cancellationToken);

        foreach (var stagedPath in stagedForDeletion)
        {
            try
            {
                await fileStorage.DeletePermanentlyAsync(stagedPath, cancellationToken);
            }
            catch (Exception ex)
            {
                // DB Spec §109: "إذا فشل الحذف الفعلي، Job cleanup يعيد المحاولة" — the metadata
                // is already gone, so this is a disk-space cleanup concern, not a data-integrity
                // one. A dedicated retry job is future scope; logging keeps it visible for now.
                logger.LogError(ex, "Failed to delete staged file {Path} after permanent delete of {DocumentUid}", stagedPath, documentUid);
            }
        }

        await auditService.RecordAsync(
            new AuditEntry(
                AuditAction.PermanentDelete, AuditActionCategory.Document, AuditResult.Success,
                UserId: requestedBy, EntityType: "document", EntityUid: documentUid.ToString(),
                EntityNameSnapshot: document.Title),
            cancellationToken);

        return Result.Success();
    }

    private async Task<string?> CheckForDuplicateAsync(string sha256, CancellationToken cancellationToken)
    {
        var existingVersion = await unitOfWork.ExecuteAsync(
            (context, ct) => context.DocumentVersions.FindBySha256Async(sha256, ct), cancellationToken);

        if (existingVersion is null)
        {
            return null;
        }

        var owningDocument = await unitOfWork.ExecuteAsync(
            (context, ct) => context.Documents.GetByUidAsync(existingVersion.DocumentUid, ct), cancellationToken);

        return owningDocument?.ArchiveNumber;
    }
}
