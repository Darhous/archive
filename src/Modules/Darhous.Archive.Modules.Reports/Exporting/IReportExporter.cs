namespace Darhous.Archive.Modules.Reports.Exporting;

public interface IReportExporter
{
    ReportFormat Format { get; }

    string FileExtension { get; }

    Task ExportAsync(ReportDefinition report, Stream destination, CancellationToken cancellationToken);
}
