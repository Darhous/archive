using System.Security.Cryptography;

namespace Darhous.Archive.Modules.Documents.Storage;

public sealed class FileStorageService(DocumentStorageOptions options) : IFileStorageService
{
    public async Task<StagedFile> StageAsync(
        string sourcePath, Guid documentUid, int versionNo, string fileExtension, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.StagingDirectory);
        var tempPath = Path.Combine(options.StagingDirectory, $"{Guid.NewGuid():N}{fileExtension}");

        await using (var source = File.OpenRead(sourcePath))
        await using (var destination = File.Create(tempPath))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        var finalPath = BuildFinalPath(documentUid, versionNo, fileExtension);
        return new StagedFile(tempPath, finalPath, documentUid, versionNo, fileExtension);
    }

    public async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    public Task CommitAsync(StagedFile stagedFile, CancellationToken cancellationToken)
    {
        if (File.Exists(stagedFile.FinalPath))
        {
            // Idempotent recovery path: a previous attempt already got the file to its final
            // location (crash happened between the move and reporting success upstream).
            if (File.Exists(stagedFile.TempPath))
            {
                File.Delete(stagedFile.TempPath);
            }

            return Task.CompletedTask;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(stagedFile.FinalPath)!);
        File.Move(stagedFile.TempPath, stagedFile.FinalPath);
        return Task.CompletedTask;
    }

    public Task RollbackStagingAsync(StagedFile stagedFile, CancellationToken cancellationToken)
    {
        if (File.Exists(stagedFile.TempPath))
        {
            File.Delete(stagedFile.TempPath);
        }

        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(File.OpenRead(filePath));

    public Task<string> MoveToRecycleStagingAsync(string filePath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.RecycleStagingDirectory);
        var stagedPath = Path.Combine(options.RecycleStagingDirectory, $"{Guid.NewGuid():N}{Path.GetExtension(filePath)}");
        File.Move(filePath, stagedPath);
        return Task.FromResult(stagedPath);
    }

    public Task RestoreFromRecycleStagingAsync(string stagedPath, string originalPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
        File.Move(stagedPath, originalPath);
        return Task.CompletedTask;
    }

    public Task DeletePermanentlyAsync(string stagedPath, CancellationToken cancellationToken)
    {
        if (File.Exists(stagedPath))
        {
            File.Delete(stagedPath);
        }

        return Task.CompletedTask;
    }

    public bool Exists(string filePath) => File.Exists(filePath);

    private string BuildFinalPath(Guid documentUid, int versionNo, string fileExtension)
    {
        var now = DateTimeOffset.UtcNow;
        var directory = Path.Combine(
            options.ArchiveStorageRoot, "Documents", now.Year.ToString("D4"), now.Month.ToString("D2"),
            documentUid.ToString(), $"v{versionNo}");

        return Path.Combine(directory, $"{documentUid}_{versionNo}{fileExtension}");
    }
}
