namespace Darhous.Archive.Modules.Folders.Tests;

public class FolderServiceTests : FoldersTestBase
{
    [Fact]
    public async Task CreateFolderAsync_RootLevel_Succeeds()
    {
        var result = await FolderService.CreateFolderAsync("المرور", null, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateFolderAsync_DuplicateSiblingName_IsBlocked()
    {
        await FolderService.CreateFolderAsync("المرور", null, null, CancellationToken.None);

        var result = await FolderService.CreateFolderAsync("المرور", null, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("FOLDER_NAME_TAKEN", result.Error!.Code);
    }

    [Fact]
    public async Task CreateFolderAsync_SameNameUnderDifferentParents_Succeeds()
    {
        var parent1 = await FolderService.CreateFolderAsync("قسم أ", null, null, CancellationToken.None);
        var parent2 = await FolderService.CreateFolderAsync("قسم ب", null, null, CancellationToken.None);

        var child1 = await FolderService.CreateFolderAsync("تقارير", parent1.Value, null, CancellationToken.None);
        var child2 = await FolderService.CreateFolderAsync("تقارير", parent2.Value, null, CancellationToken.None);

        Assert.True(child1.IsSuccess);
        Assert.True(child2.IsSuccess);
    }

    [Fact]
    public async Task RenameFolderAsync_ToOwnCurrentName_Succeeds()
    {
        var folder = await FolderService.CreateFolderAsync("الأصلي", null, null, CancellationToken.None);

        var result = await FolderService.RenameFolderAsync(folder.Value, "الأصلي", CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RenameFolderAsync_ToSiblingsName_IsBlocked()
    {
        await FolderService.CreateFolderAsync("موجود", null, null, CancellationToken.None);
        var toRename = await FolderService.CreateFolderAsync("قابل للتغيير", null, null, CancellationToken.None);

        var result = await FolderService.RenameFolderAsync(toRename.Value, "موجود", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("FOLDER_NAME_TAKEN", result.Error!.Code);
    }

    [Fact]
    public async Task MoveFolderAsync_IntoItself_IsRejected()
    {
        var folder = await FolderService.CreateFolderAsync("أ", null, null, CancellationToken.None);

        var result = await FolderService.MoveFolderAsync(folder.Value, folder.Value, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("FOLDER_MOVE_CYCLE", result.Error!.Code);
    }

    [Fact]
    public async Task MoveFolderAsync_IntoOwnDescendant_IsRejected()
    {
        var grandparent = await FolderService.CreateFolderAsync("جد", null, null, CancellationToken.None);
        var parent = await FolderService.CreateFolderAsync("أب", grandparent.Value, null, CancellationToken.None);
        var child = await FolderService.CreateFolderAsync("ابن", parent.Value, null, CancellationToken.None);

        var result = await FolderService.MoveFolderAsync(grandparent.Value, child.Value, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("FOLDER_MOVE_CYCLE", result.Error!.Code);
    }

    [Fact]
    public async Task MoveFolderAsync_ToUnrelatedFolder_Succeeds()
    {
        var a = await FolderService.CreateFolderAsync("أ", null, null, CancellationToken.None);
        var b = await FolderService.CreateFolderAsync("ب", null, null, CancellationToken.None);

        var result = await FolderService.MoveFolderAsync(a.Value, b.Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var children = await FolderService.GetChildrenAsync(b.Value, CancellationToken.None);
        Assert.Contains(children, f => f.Uid == a.Value);
    }

    [Fact]
    public async Task DeleteFolderAsync_WithSubfolders_IsBlockedByDefault()
    {
        var parent = await FolderService.CreateFolderAsync("أب", null, null, CancellationToken.None);
        await FolderService.CreateFolderAsync("ابن", parent.Value, null, CancellationToken.None);

        var result = await FolderService.DeleteFolderAsync(parent.Value, moveContentsToUnclassified: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("FOLDER_NOT_EMPTY", result.Error!.Code);
    }

    [Fact]
    public async Task DeleteFolderAsync_WithDocuments_IsBlockedByDefault()
    {
        var folder = await FolderService.CreateFolderAsync("يحتوي مستندات", null, null, CancellationToken.None);
        await AddDocumentAsync("مستند", folder.Value);

        var result = await FolderService.DeleteFolderAsync(folder.Value, moveContentsToUnclassified: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("FOLDER_NOT_EMPTY", result.Error!.Code);
    }

    [Fact]
    public async Task DeleteFolderAsync_Empty_Succeeds()
    {
        var folder = await FolderService.CreateFolderAsync("فاضي", null, null, CancellationToken.None);

        var result = await FolderService.DeleteFolderAsync(folder.Value, moveContentsToUnclassified: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteFolderAsync_WithMoveContents_MovesDocumentsToUnclassifiedAndDeletes()
    {
        var folder = await FolderService.CreateFolderAsync("للحذف", null, null, CancellationToken.None);
        var docUid = await AddDocumentAsync("مستند منقول", folder.Value);

        var result = await FolderService.DeleteFolderAsync(folder.Value, moveContentsToUnclassified: true, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var document = await UnitOfWork.ExecuteAsync((context, ct) => context.Documents.GetByUidAsync(docUid, ct), CancellationToken.None);
        Assert.Null(document!.FolderId);
    }

    [Fact]
    public async Task DeleteFolderAsync_UnknownFolder_ReturnsNotFound()
    {
        var result = await FolderService.DeleteFolderAsync(Guid.NewGuid(), false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("FOLDER_NOT_FOUND", result.Error!.Code);
    }
}
