using Darhous.Archive.Core.Hosting;

namespace Darhous.Archive.Configuration;

/// <summary>
/// Implementation Plan §92-93 — the fixed folder layout under %ProgramData% (shared,
/// machine-wide state) and %LocalAppData% (per-user UI preferences only).
/// </summary>
public static class AppPaths
{
    public static string ProgramDataRoot => ArchiveHostDefaults.ProgramDataRoot;

    public static string Data => Path.Combine(ProgramDataRoot, "Data");
    public static string Logs => Path.Combine(ProgramDataRoot, "Logs");
    public static string Plugins => Path.Combine(ProgramDataRoot, "Plugins");
    public static string PluginData => Path.Combine(ProgramDataRoot, "PluginData");
    public static string ArchiveStorage => Path.Combine(ProgramDataRoot, "ArchiveStorage");
    public static string Backups => Path.Combine(ProgramDataRoot, "Backups");
    public static string Temp => Path.Combine(ProgramDataRoot, "Temp");
    public static string Quarantine => Path.Combine(ProgramDataRoot, "Quarantine");
    public static string Updates => Path.Combine(ProgramDataRoot, "Updates");

    /// <summary>%LocalAppData%\DarhousSmartArchive — per-user, non-shared UI preferences.</summary>
    public static string UserSettingsRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DarhousSmartArchive");

    /// <summary>Creates every ProgramData subfolder that does not exist yet. Safe to call repeatedly.</summary>
    public static void EnsureCreated()
    {
        foreach (var path in new[]
                 {
                     Data, Logs, Plugins, PluginData, ArchiveStorage, Backups, Temp, Quarantine, Updates,
                     UserSettingsRoot,
                 })
        {
            Directory.CreateDirectory(path);
        }
    }
}
