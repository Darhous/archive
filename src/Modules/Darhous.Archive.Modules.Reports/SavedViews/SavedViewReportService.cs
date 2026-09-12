using Darhous.Archive.Modules.Reports.Exporting;

namespace Darhous.Archive.Modules.Reports.SavedViews;

public sealed class SavedViewReportService(IReportExportService exportService) : ISavedViewReportService
{
    private static readonly string[] Columns = ["Archive number", "Title", "Status", "Archive date"];

    public Task ExportAsync(
        SavedViewReport view,
        ReportFormat format,
        string outputPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(view.Name);
        ArgumentNullException.ThrowIfNull(view.Documents);

        var subtitle = string.IsNullOrWhiteSpace(view.Filter)
            ? $"Exported rows={view.Documents.Count}"
            : $"Filter: {view.Filter}; Exported rows={view.Documents.Count}";
        var report = new ReportDefinition(
            view.Name,
            Columns,
            view.Documents.Select(document => new string?[]
            {
                document.ArchiveNumber,
                document.Title,
                document.Status,
                document.ArchiveDate,
            }),
            subtitle);

        return exportService.ExportToFileAsync(report, format, outputPath, cancellationToken);
    }
}
