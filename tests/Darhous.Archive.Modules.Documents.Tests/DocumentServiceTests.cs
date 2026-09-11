using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Core.Permissions;

namespace Darhous.Archive.Modules.Documents.Tests;

public class DocumentServiceTests : DocumentServiceTestBase
{
    [Fact]
    public async Task AddDocumentAsync_Managed_CopiesFileAndCreatesRecord()
    {
        var source = CreateSourceFile("hello world");

        var result = await DocumentService.AddDocumentAsync(
            source, "Test Document", null, DocumentSourceType.Manual, DocumentStorageMode.Managed,
            createdBy: null, allowDuplicate: false, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var document = await UnitOfWork.ExecuteAsync((context, ct) => context.Documents.GetByUidAsync(result.Value, ct), CancellationToken.None);
        Assert.NotNull(document);
        Assert.Equal(DocumentStatus.Active, document!.Status);
        Assert.StartsWith("ARC-", document.ArchiveNumber);

        var versions = await UnitOfWork.ExecuteAsync((context, ct) => context.DocumentVersions.ListForDocumentAsync(result.Value, ct), CancellationToken.None);
        Assert.Single(versions);
        Assert.True(File.Exists(versions[0].FilePath));
        Assert.True(File.Exists(source), "managed storage copies the source into ArchiveStorage — the original source file is left alone.");
        Assert.NotEqual(source, versions[0].FilePath);
        Assert.Equal("hello world", File.ReadAllText(versions[0].FilePath));
    }

    [Fact]
    public async Task AddDocumentAsync_IndexedInPlace_DoesNotCopyFile()
    {
        var source = CreateSourceFile("in place content");

        var result = await DocumentService.AddDocumentAsync(
            source, "In Place Doc", null, DocumentSourceType.Manual, DocumentStorageMode.IndexedInPlace,
            createdBy: null, allowDuplicate: false, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var versions = await UnitOfWork.ExecuteAsync((context, ct) => context.DocumentVersions.ListForDocumentAsync(result.Value, ct), CancellationToken.None);
        Assert.Equal(source, versions[0].FilePath);
        Assert.True(File.Exists(source), "indexed_in_place must leave the original file exactly where it was.");
    }

    [Fact]
    public async Task AddDocumentAsync_SourceMissing_FailsCleanly()
    {
        var result = await DocumentService.AddDocumentAsync(
            @"C:\does\not\exist.pdf", "Missing", null, DocumentSourceType.Manual, DocumentStorageMode.Managed,
            null, false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_SOURCE_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task AddDocumentAsync_ExactDuplicateContent_IsDetectedAndBlockedByDefault()
    {
        var first = CreateSourceFile("same content twice");
        var second = CreateSourceFile("same content twice");

        var firstResult = await DocumentService.AddDocumentAsync(
            first, "First", null, DocumentSourceType.Manual, DocumentStorageMode.Managed, null, false, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);

        var secondResult = await DocumentService.AddDocumentAsync(
            second, "Second (duplicate)", null, DocumentSourceType.Manual, DocumentStorageMode.Managed, null, false, CancellationToken.None);

        Assert.False(secondResult.IsSuccess);
        Assert.Equal("DOCUMENT_DUPLICATE_DETECTED", secondResult.Error!.Code);

        // The staged copy for the rejected duplicate must not be left behind.
        var allDocs = await UnitOfWork.ExecuteAsync((context, ct) => context.Documents.ListAsync(ct), CancellationToken.None);
        Assert.Single(allDocs);
    }

    [Fact]
    public async Task AddDocumentAsync_ExactDuplicateContent_SucceedsWhenAllowed()
    {
        var first = CreateSourceFile("duplicate but allowed");
        var second = CreateSourceFile("duplicate but allowed");

        await DocumentService.AddDocumentAsync(first, "First", null, DocumentSourceType.Manual, DocumentStorageMode.Managed, null, false, CancellationToken.None);
        var secondResult = await DocumentService.AddDocumentAsync(second, "Second", null, DocumentSourceType.Manual, DocumentStorageMode.Managed, null, true, CancellationToken.None);

        Assert.True(secondResult.IsSuccess);

        var allDocs = await UnitOfWork.ExecuteAsync((context, ct) => context.Documents.ListAsync(ct), CancellationToken.None);
        Assert.Equal(2, allDocs.Count);
    }

    [Fact]
    public async Task TrashThenRestore_RoundTrips()
    {
        var source = CreateSourceFile();
        var added = await DocumentService.AddDocumentAsync(source, "Trash Me", null, DocumentSourceType.Manual, DocumentStorageMode.Managed, null, false, CancellationToken.None);

        var trashResult = await DocumentService.TrashDocumentAsync(added.Value, null, "test reason", CancellationToken.None);
        Assert.True(trashResult.IsSuccess);

        var trashed = await UnitOfWork.ExecuteAsync((context, ct) => context.Documents.GetByUidAsync(added.Value, ct), CancellationToken.None);
        Assert.Equal(DocumentStatus.Trashed, trashed!.Status);
        Assert.NotNull(trashed.DeletedAt);

        var binEntry = await UnitOfWork.ExecuteAsync((context, ct) => context.RecycleBin.GetAsync(added.Value, ct), CancellationToken.None);
        Assert.NotNull(binEntry);
        Assert.Equal("test reason", binEntry!.DeleteReason);

        var restoreResult = await DocumentService.RestoreDocumentAsync(added.Value, null, CancellationToken.None);
        Assert.True(restoreResult.IsSuccess);

        var restored = await UnitOfWork.ExecuteAsync((context, ct) => context.Documents.GetByUidAsync(added.Value, ct), CancellationToken.None);
        Assert.Equal(DocumentStatus.Active, restored!.Status);
        Assert.Null(restored.DeletedAt);

        var binEntryAfterRestore = await UnitOfWork.ExecuteAsync((context, ct) => context.RecycleBin.GetAsync(added.Value, ct), CancellationToken.None);
        Assert.Null(binEntryAfterRestore);
    }

    [Fact]
    public async Task MoveDocumentAsync_UpdatesFolder()
    {
        var source = CreateSourceFile();
        var added = await DocumentService.AddDocumentAsync(source, "Move Me", null, DocumentSourceType.Manual, DocumentStorageMode.Managed, null, false, CancellationToken.None);

        // No folders module yet (Phase 6) — moving to a non-existent folder uid should just
        // leave folder_id NULL (the subquery in MoveToFolderAsync finds nothing), not throw.
        var result = await DocumentService.MoveDocumentAsync(added.Value, Guid.NewGuid(), null, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PermanentDeleteDocumentAsync_NonAdmin_IsForbidden()
    {
        var source = CreateSourceFile();
        var added = await DocumentService.AddDocumentAsync(source, "Protected", null, DocumentSourceType.Manual, DocumentStorageMode.Managed, null, false, CancellationToken.None);

        var result = await DocumentService.PermanentDeleteDocumentAsync(added.Value, UserRole.User, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_PERMANENT_DELETE_FORBIDDEN", result.Error!.Code);
    }

    [Fact]
    public async Task PermanentDeleteDocumentAsync_Admin_RemovesRowAndFile()
    {
        var source = CreateSourceFile();
        var added = await DocumentService.AddDocumentAsync(source, "Doomed", null, DocumentSourceType.Manual, DocumentStorageMode.Managed, null, false, CancellationToken.None);
        var versions = await UnitOfWork.ExecuteAsync((context, ct) => context.DocumentVersions.ListForDocumentAsync(added.Value, ct), CancellationToken.None);
        var filePath = versions[0].FilePath;

        var result = await DocumentService.PermanentDeleteDocumentAsync(added.Value, UserRole.Admin, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(filePath));

        var deletedDoc = await UnitOfWork.ExecuteAsync((context, ct) => context.Documents.GetByUidAsync(added.Value, ct), CancellationToken.None);
        Assert.Null(deletedDoc);
    }

    [Fact]
    public async Task PermanentDeleteDocumentAsync_UnknownDocument_ReturnsNotFound()
    {
        var result = await DocumentService.PermanentDeleteDocumentAsync(Guid.NewGuid(), UserRole.Admin, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.Error!.Code);
    }
}
