namespace Darhous.Archive.Modules.Reports.Exporting;

public interface IReportExportService
{
    Task ExportToFileAsync(
        ReportDefinition report,
        ReportFormat format,
        string outputPath,
        CancellationToken cancellationToken);
}
