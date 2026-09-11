using Darhous.Archive.Configuration;

namespace Darhous.Archive.Modules.Documents.Storage;

/// <summary>Defaults to <see cref="AppPaths.ArchiveStorage"/>; tests override to an isolated temp directory.</summary>
public sealed class DocumentStorageOptions
{
    public string ArchiveStorageRoot { get; init; } = AppPaths.ArchiveStorage;

    public string StagingDirectory => Path.Combine(ArchiveStorageRoot, ".staging");

    public string RecycleStagingDirectory => Path.Combine(ArchiveStorageRoot, ".recycle-staging");
}
