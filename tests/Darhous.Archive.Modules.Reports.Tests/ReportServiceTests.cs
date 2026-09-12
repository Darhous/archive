using Darhous.Archive.Contracts.Audit;
using Darhous.Archive.Core.Audit;
using Darhous.Archive.Modules.Reports.Audit;
using Darhous.Archive.Modules.Reports.Exporting;
using Darhous.Archive.Modules.Reports.SavedViews;

namespace Darhous.Archive.Modules.Reports.Tests;

public sealed class ReportServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "darhous-report-tests", Guid.NewGuid().ToString("N"));

    public ReportServiceTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task ReportExportService_SelectsExporterAndAtomicallyReplacesOutput()
    {
        var exporter = new StubExporter(ReportFormat.Csv, ".csv", "new content");
        var service = new ReportExportService([exporter]);
        var outputPath = Path.Combine(_directory, "report.csv");
        await File.WriteAllTextAsync(outputPath, "old content");
        var report = new ReportDefinition("Test", ["Column"], new string?[][] { ["Value"] });

        await service.ExportToFileAsync(report, ReportFormat.Csv, outputPath, CancellationToken.None);

        Assert.Equal("new content", await File.ReadAllTextAsync(outputPath));
        Assert.Same(report, exporter.ReceivedReport);
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task ReportExportService_RejectsMismatchedExtension()
    {
        var service = new ReportExportService([new StubExporter(ReportFormat.Csv, ".csv", "content")]);
        var report = new ReportDefinition("Test", ["Column"], Array.Empty<string?[]>());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExportToFileAsync(report, ReportFormat.Csv, Path.Combine(_directory, "report.pdf"), CancellationToken.None));
    }

    [Fact]
    public async Task AuditReportService_QueriesAndMapsCompleteAuditRows()
    {
        var auditEvent = CreateAuditEvent();
        var queryService = new StubAuditQueryService([auditEvent]);
        var exportService = new CapturingExportService();
        var service = new AuditReportService(queryService, exportService);
        var query = new AuditQuery(Action: "document.viewed", Skip: 10, Take: 25);
        var outputPath = Path.Combine(_directory, "audit.xlsx");

        await service.ExportAsync(query, ReportFormat.Excel, outputPath, CancellationToken.None);

        Assert.Same(query, queryService.ReceivedQuery);
        Assert.Equal(ReportFormat.Excel, exportService.Format);
        Assert.Equal(outputPath, exportService.OutputPath);
        Assert.Equal(22, exportService.Report!.Columns.Count);
        Assert.Single(exportService.Report.Rows);
        Assert.Equal(auditEvent.Uid.ToString(), exportService.Report.Rows[0][0]);
        Assert.Equal("document.viewed", exportService.Report.Rows[0][5]);
        Assert.Equal("Failure", exportService.Report.Rows[0][16]);
        Assert.Contains("Exported rows=1", exportService.Report.Subtitle);
    }

    [Fact]
    public async Task SavedViewReportService_ExportsExactlyTheProvidedCurrentRowsAndFilter()
    {
        var exportService = new CapturingExportService();
        var service = new SavedViewReportService(exportService);
        var view = new SavedViewReport(
            "All archive > Legal",
            "عقد",
            [new SavedViewDocument("A-001", "عقد", "Active", "2026-09-12")]);

        await service.ExportAsync(view, ReportFormat.Pdf, Path.Combine(_directory, "view.pdf"), CancellationToken.None);

        Assert.Equal("All archive > Legal", exportService.Report!.Title);
        Assert.Contains("Filter: عقد", exportService.Report.Subtitle);
        Assert.Equal(["A-001", "عقد", "Active", "2026-09-12"], exportService.Report.Rows.Single());
    }

    private static AuditEvent CreateAuditEvent() => new(
        Guid.CreateVersion7(),
        new DateTimeOffset(2026, 9, 12, 10, 30, 0, TimeSpan.Zero),
        Guid.CreateVersion7(),
        "ahmed",
        "Admin",
        "document.viewed",
        AuditActionCategory.DocumentView,
        "document",
        "DOC-1",
        "Contract",
        "details",
        "query",
        "timestamp desc",
        "status=active",
        "{}",
        "{}",
        AuditResult.Failure,
        "E-1",
        Guid.CreateVersion7(),
        "job-1",
        "plugin-1",
        "WORKSTATION");

    private sealed class StubExporter(ReportFormat format, string extension, string content) : IReportExporter
    {
        public ReportFormat Format => format;

        public string FileExtension => extension;

        public ReportDefinition? ReceivedReport { get; private set; }

        public async Task ExportAsync(ReportDefinition report, Stream destination, CancellationToken cancellationToken)
        {
            ReceivedReport = report;
            await using var writer = new StreamWriter(destination, leaveOpen: true);
            await writer.WriteAsync(content.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
    }

    private sealed class StubAuditQueryService(IReadOnlyList<AuditEvent> events) : IAuditQueryService
    {
        public AuditQuery? ReceivedQuery { get; private set; }

        public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
        {
            ReceivedQuery = query;
            return Task.FromResult(events);
        }
    }

    private sealed class CapturingExportService : IReportExportService
    {
        public ReportDefinition? Report { get; private set; }

        public ReportFormat Format { get; private set; }

        public string? OutputPath { get; private set; }

        public Task ExportToFileAsync(
            ReportDefinition report,
            ReportFormat format,
            string outputPath,
            CancellationToken cancellationToken)
        {
            Report = report;
            Format = format;
            OutputPath = outputPath;
            return Task.CompletedTask;
        }
    }
}
