using System.Windows;
using Microsoft.Win32;
using Darhous.Archive.Configuration;

namespace Darhous.Archive.Desktop.Themes;

/// <summary>
/// Swaps the Colors.Light.xaml/Colors.Dark.xaml dictionary registered in App.xaml — every
/// other theme resource (Controls.xaml, Typography.xaml) references those color keys via
/// DynamicResource, so this one swap re-themes the whole app live.
/// </summary>
public static class ThemeManager
{
    private const string LightDictionaryUri = "Themes/Colors.Light.xaml";
    private const string DarkDictionaryUri = "Themes/Colors.Dark.xaml";

    public static void Apply(AppTheme theme)
    {
        var resolvedTheme = theme == AppTheme.System ? ResolveSystemTheme() : theme;
        var targetUri = resolvedTheme == AppTheme.Dark ? DarkDictionaryUri : LightDictionaryUri;

        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(d =>
            d.Source is not null && (d.Source.OriginalString.EndsWith("Colors.Light.xaml") || d.Source.OriginalString.EndsWith("Colors.Dark.xaml")));

        var newDictionary = new ResourceDictionary { Source = new Uri(targetUri, UriKind.Relative) };

        if (existing is not null)
        {
            var index = dictionaries.IndexOf(existing);
            dictionaries.RemoveAt(index);
            dictionaries.Insert(index, newDictionary);
        }
        else
        {
            dictionaries.Add(newDictionary);
        }
    }

    /// <summary>
    /// Reads the standard Windows personalization key. Falls back to Light if the key is
    /// missing or unreadable (a locked-down machine, or a Windows version predating this
    /// setting) rather than throwing — theme selection must never block startup.
    /// </summary>
    private static AppTheme ResolveSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue)
            {
                return intValue == 0 ? AppTheme.Dark : AppTheme.Light;
            }
        }
        catch (Exception)
        {
            // Registry access can fail under restrictive policies — Light is a safe default.
        }

        return AppTheme.Light;
    }
}
