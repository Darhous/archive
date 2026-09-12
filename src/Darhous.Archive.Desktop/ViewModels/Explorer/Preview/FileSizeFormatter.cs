namespace Darhous.Archive.Desktop.ViewModels.Explorer.Preview;

internal static class FileSizeFormatter
{
    private static readonly string[] Units = ["بايت", "ك.ب", "م.ب", "ج.ب"];

    public static string Format(long bytes)
    {
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{bytes} {Units[0]}" : $"{size:0.#} {Units[unitIndex]}";
    }
}
