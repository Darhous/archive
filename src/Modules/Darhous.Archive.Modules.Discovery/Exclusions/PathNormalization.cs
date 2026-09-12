namespace Darhous.Archive.Modules.Discovery.Exclusions;

/// <summary>Same normalization rule as <c>Darhous.Archive.Persistence.Repositories.SourceExclusionRepository</c> (kept in sync manually — trivial and unlikely to drift, not worth a shared package for one line).</summary>
internal static class PathNormalization
{
    public static string Normalize(string path) => path.Replace('/', '\\').TrimEnd('\\').ToUpperInvariant();
}
