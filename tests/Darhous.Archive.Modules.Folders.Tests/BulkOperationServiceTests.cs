namespace Darhous.Archive.Modules.Folders.Tests;

public class BulkOperationServiceTests : FoldersTestBase
{
    [Fact]
    public async Task BulkMoveDocumentsAsync_MovesAllDocuments()
    {
        var target = await FolderService.CreateFolderAsync("الهدف", null, null, CancellationToken.None);
        var doc1 = await AddDocumentAsync("مستند 1");
        var doc2 = await AddDocumentAsync("مستند 2");

        var result = await BulkOperationService.BulkMoveDocumentsAsync([doc1, doc2], target.Value, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var d1 = await UnitOfWork.ExecuteAsync((c, ct) => c.Documents.GetByUidAsync(doc1, ct), CancellationToken.None);
        var d2 = await UnitOfWork.ExecuteAsync((c, ct) => c.Documents.GetByUidAsync(doc2, ct), CancellationToken.None);
        Assert.Equal(target.Value, d1!.FolderId);
        Assert.Equal(target.Value, d2!.FolderId);
    }

    [Fact]
    public async Task BulkMoveDocumentsAsync_EmptyList_Fails()
    {
        var result = await BulkOperationService.BulkMoveDocumentsAsync([], null, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("BULK_MOVE_EMPTY", result.Error!.Code);
    }

    [Fact]
    public async Task UndoAsync_WithinWindow_RevertsToOriginalFolders()
    {
        var originalFolder = await FolderService.CreateFolderAsync("الأصلي", null, null, CancellationToken.None);
        var targetFolder = await FolderService.CreateFolderAsync("الهدف", null, null, CancellationToken.None);
        var doc = await AddDocumentAsync("مستند", originalFolder.Value);

        var moveResult = await BulkOperationService.BulkMoveDocumentsAsync([doc], targetFolder.Value, null, CancellationToken.None);
        Assert.True(moveResult.IsSuccess);

        var undoResult = await BulkOperationService.UndoAsync(moveResult.Value, CancellationToken.None);

        Assert.True(undoResult.IsSuccess);
        var document = await UnitOfWork.ExecuteAsync((c, ct) => c.Documents.GetByUidAsync(doc, ct), CancellationToken.None);
        Assert.Equal(originalFolder.Value, document!.FolderId);
    }

    [Fact]
    public async Task UndoAsync_CalledTwice_SecondCallFails()
    {
        var doc = await AddDocumentAsync("مستند");
        var target = await FolderService.CreateFolderAsync("الهدف", null, null, CancellationToken.None);
        var moveResult = await BulkOperationService.BulkMoveDocumentsAsync([doc], target.Value, null, CancellationToken.None);

        await BulkOperationService.UndoAsync(moveResult.Value, CancellationToken.None);
        var secondUndo = await BulkOperationService.UndoAsync(moveResult.Value, CancellationToken.None);

        Assert.False(secondUndo.IsSuccess);
        Assert.Equal("UNDO_WINDOW_EXPIRED", secondUndo.Error!.Code);
    }

    [Fact]
    public async Task UndoAsync_UnknownSnapshot_Fails()
    {
        var result = await BulkOperationService.UndoAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("UNDO_SNAPSHOT_NOT_FOUND", result.Error!.Code);
    }
}
