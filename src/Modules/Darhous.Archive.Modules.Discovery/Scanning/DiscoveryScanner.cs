using Darhous.Archive.Modules.Discovery.Exclusions;

namespace Darhous.Archive.Modules.Discovery.Scanning;

/// <summary>
/// Implementation Plan §52 (Discovery Safety): never opens/reads file content, never modifies
/// anything, never follows reparse points (junctions/symlinks — the classic infinite-loop
/// vector), and never crashes the whole scan because one subfolder denies access. Explicit
/// stack-based traversal rather than recursion — a pathologically deep folder tree must not
/// risk a stack overflow.
/// </summary>
public sealed class DiscoveryScanner : IDiscoveryScanner
{
    public ScanResult Scan(string rootPath, bool includeSubfolders, ExclusionSet exclusions, CancellationToken cancellationToken)
    {
        var supportedFiles = new List<DiscoveredFile>();
        int filesSeen = 0, skipped = 0, errors = 0;

        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directoryPath = pending.Pop();

            if (exclusions.IsPathExcluded(directoryPath) || exclusions.IsDirectoryNameExcluded(Path.GetFileName(directoryPath)))
            {
                skipped++;
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directoryPath);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                errors++;
                continue;
            }

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                filesSeen++;

                if (exclusions.IsPathExcluded(filePath))
                {
                    skipped++;
                    continue;
                }

                var extension = Path.GetExtension(filePath);
                if (!DiscoveryDefaults.SupportedExtensions.Contains(extension))
                {
                    continue;
                }

                try
                {
                    var info = new FileInfo(filePath);
                    supportedFiles.Add(new DiscoveredFile(filePath, extension, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc));
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    errors++;
                }
            }

            if (!includeSubfolders)
            {
                continue;
            }

            IEnumerable<string> subdirectories;
            try
            {
                subdirectories = Directory.EnumerateDirectories(directoryPath);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                errors++;
                continue;
            }

            foreach (var subdirectory in subdirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileAttributes attributes;
                try
                {
                    attributes = new DirectoryInfo(subdirectory).Attributes;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    errors++;
                    continue;
                }

                // Never follow reparse points (junctions/symlinks) — Implementation Plan §46.3:
                // prevents infinite scan loops from a junction pointing back up the tree.
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                pending.Push(subdirectory);
            }
        }

        return new ScanResult(supportedFiles, filesSeen, skipped, errors);
    }
}
