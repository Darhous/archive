namespace Darhous.Archive.Modules.Reports.Exporting;

public sealed class ReportExportService : IReportExportService
{
    private readonly IReadOnlyDictionary<ReportFormat, IReportExporter> _exporters;

    public ReportExportService(IEnumerable<IReportExporter> exporters)
    {
        ArgumentNullException.ThrowIfNull(exporters);
        _exporters = exporters.ToDictionary(exporter => exporter.Format);
    }

    public async Task ExportToFileAsync(
        ReportDefinition report,
        ReportFormat format,
        string outputPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (!_exporters.TryGetValue(format, out var exporter))
        {
            throw new NotSupportedException($"No report exporter is registered for {format}.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullPath), exporter.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"The output path must use the {exporter.FileExtension} extension for {format} reports.",
                nameof(outputPath));
        }

        var directory = Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"The report output directory does not exist: {directory}");
        }

        var temporaryPath = $"{fullPath}.{Guid.CreateVersion7():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await exporter.ExportAsync(report, stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }
}
