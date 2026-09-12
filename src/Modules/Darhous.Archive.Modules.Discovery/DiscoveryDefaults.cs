using Darhous.Archive.Configuration;

namespace Darhous.Archive.Modules.Discovery;

/// <summary>Implementation Plan §47-48 (Supported Extensions, Technical Exclusions).</summary>
public static class DiscoveryDefaults
{
    public static readonly IReadOnlySet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".msg", ".eml",
    };

    /// <summary>
    /// Absolute paths, seeded once per machine on startup (idempotent — <c>ISourceExclusionRepository.CreateIfMissingAsync</c>).
    /// %SystemRoot%/%ProgramFiles% resolved at seed time since they vary by machine/locale;
    /// the rest are relative markers matched by name wherever they appear during enumeration.
    /// </summary>
    public static IReadOnlyList<string> GetTechnicalExclusionRoots() =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        AppPaths.ProgramDataRoot, // "Darhous internal directories" (Implementation Plan §48) — never re-discover our own managed storage/logs/plugin data as if it were user content.
    ];

    /// <summary>Directory names excluded wherever they appear (not just at drive root) — System Volume Information/$Recycle.Bin exist per-volume.</summary>
    public static readonly IReadOnlySet<string> ExcludedDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "System Volume Information", "$Recycle.Bin", "Temp",
    };
}
