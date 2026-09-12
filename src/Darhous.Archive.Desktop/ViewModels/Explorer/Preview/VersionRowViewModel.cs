using Darhous.Archive.Application.Persistence;

namespace Darhous.Archive.Desktop.ViewModels.Explorer.Preview;

/// <summary>One row in the Preview pane's Versions tab (SAD §21).</summary>
public sealed class VersionRowViewModel(DocumentVersion version)
{
    public int VersionNo { get; } = version.VersionNo;
    public string OriginalFileName { get; } = version.OriginalFileName;
    public string ImportedAt { get; } = version.ImportedAt.ToString("yyyy-MM-dd HH:mm");
    public string FileSizeDisplay { get; } = FileSizeFormatter.Format(version.FileSize);
    public string AvailabilityStatus { get; } = version.AvailabilityStatus.ToString();
}
