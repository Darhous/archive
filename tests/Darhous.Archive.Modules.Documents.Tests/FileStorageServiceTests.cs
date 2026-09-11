using Darhous.Archive.Modules.Documents.Storage;

namespace Darhous.Archive.Modules.Documents.Tests;

public class FileStorageServiceTests : DocumentServiceTestBase
{
    [Fact]
    public async Task StageThenCommit_MovesFileToDeterministicFinalPath()
    {
        var source = CreateSourceFile("content");
        var documentUid = Guid.NewGuid();

        var staged = await FileStorage.StageAsync(source, documentUid, 1, ".pdf", CancellationToken.None);
        Assert.True(File.Exists(staged.TempPath));
        Assert.Contains(documentUid.ToString(), staged.FinalPath);

        await FileStorage.CommitAsync(staged, CancellationToken.None);

        Assert.False(File.Exists(staged.TempPath));
        Assert.True(File.Exists(staged.FinalPath));
    }

    [Fact]
    public async Task Commit_IsIdempotent_WhenFinalFileAlreadyExists()
    {
        var source = CreateSourceFile("content");
        var documentUid = Guid.NewGuid();
        var staged = await FileStorage.StageAsync(source, documentUid, 1, ".pdf", CancellationToken.None);
        await FileStorage.CommitAsync(staged, CancellationToken.None);

        // Simulate a retry after a crash: stage again with the same identity, commit again.
        var restaged = await FileStorage.StageAsync(source, documentUid, 1, ".pdf", CancellationToken.None);
        await FileStorage.CommitAsync(restaged, CancellationToken.None);

        Assert.True(File.Exists(restaged.FinalPath));
        Assert.False(File.Exists(restaged.TempPath), "the redundant staged copy must be cleaned up, not left behind");
    }

    [Fact]
    public async Task RollbackStaging_DeletesTempFile_LeavesNothingElse()
    {
        var source = CreateSourceFile("content");
        var staged = await FileStorage.StageAsync(source, Guid.NewGuid(), 1, ".pdf", CancellationToken.None);

        await FileStorage.RollbackStagingAsync(staged, CancellationToken.None);

        Assert.False(File.Exists(staged.TempPath));
        Assert.False(File.Exists(staged.FinalPath));
    }

    [Fact]
    public async Task ComputeSha256Async_SameContent_SameHash()
    {
        var a = CreateSourceFile("identical content");
        var b = CreateSourceFile("identical content");

        var hashA = await FileStorage.ComputeSha256Async(a, CancellationToken.None);
        var hashB = await FileStorage.ComputeSha256Async(b, CancellationToken.None);

        Assert.Equal(hashA, hashB);
        Assert.Equal(64, hashA.Length);
    }

    [Fact]
    public async Task MoveToRecycleStaging_ThenRestore_RoundTrips()
    {
        var source = CreateSourceFile("recoverable");
        var originalPath = Path.Combine(Path.GetDirectoryName(source)!, "live_copy.pdf");
        File.Copy(source, originalPath);

        var stagedPath = await FileStorage.MoveToRecycleStagingAsync(originalPath, CancellationToken.None);
        Assert.False(File.Exists(originalPath));
        Assert.True(File.Exists(stagedPath));

        await FileStorage.RestoreFromRecycleStagingAsync(stagedPath, originalPath, CancellationToken.None);
        Assert.True(File.Exists(originalPath));
        Assert.False(File.Exists(stagedPath));
    }

    [Fact]
    public async Task MoveToRecycleStaging_ThenDeletePermanently_RemovesFile()
    {
        var source = CreateSourceFile("gone forever");
        var originalPath = Path.Combine(Path.GetDirectoryName(source)!, "to_delete.pdf");
        File.Copy(source, originalPath);

        var stagedPath = await FileStorage.MoveToRecycleStagingAsync(originalPath, CancellationToken.None);
        await FileStorage.DeletePermanentlyAsync(stagedPath, CancellationToken.None);

        Assert.False(File.Exists(stagedPath));
    }
}
