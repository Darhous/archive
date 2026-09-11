namespace Darhous.Archive.Configuration;

/// <summary>SAD §63 — Updates settings (Manual vs. Automatic checking/installing).</summary>
public sealed record UpdateOptions(bool CheckOnStartup = true, bool AutoInstall = false);
