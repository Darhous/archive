namespace Darhous.Archive.Modules.Discovery.Exclusions;

/// <summary>
/// Immutable snapshot loaded once per scan (not re-queried per file — a full-disk enumeration
/// can touch millions of paths). Excludes by (a) normalized-path prefix match against
/// technical/user exclusions and (b) directory name, for names like "$Recycle.Bin" that
/// legitimately appear on every volume rather than at one fixed path.
/// </summary>
public sealed class ExclusionSet(IReadOnlyList<string> normalizedPathPrefixes, IReadOnlySet<string> excludedDirectoryNames)
{
    public bool IsPathExcluded(string fullPath)
    {
        var normalized = PathNormalization.Normalize(fullPath);
        foreach (var prefix in normalizedPathPrefixes)
        {
            if (normalized == prefix || normalized.StartsWith(prefix + "\\", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsDirectoryNameExcluded(string directoryName) => excludedDirectoryNames.Contains(directoryName);
}
