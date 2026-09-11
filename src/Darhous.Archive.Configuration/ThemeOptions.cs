namespace Darhous.Archive.Configuration;

public enum AppTheme
{
    Light,
    Dark,
    System,
}

/// <summary>UI/UX Design System — theme is user-configurable, RTL is not (mandatory Arabic UI).</summary>
public sealed record ThemeOptions(AppTheme Theme = AppTheme.System);
