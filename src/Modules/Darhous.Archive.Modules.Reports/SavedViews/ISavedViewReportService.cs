namespace Darhous.Archive.Modules.Reports.SavedViews;

public interface ISavedViewReportService
{
    Task ExportAsync(
        SavedViewReport view,
        ReportFormat format,
        string outputPath,
        CancellationToken cancellationToken);
}
