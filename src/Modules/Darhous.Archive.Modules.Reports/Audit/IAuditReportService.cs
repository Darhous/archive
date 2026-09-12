using Darhous.Archive.Contracts.Audit;

namespace Darhous.Archive.Modules.Reports.Audit;

public interface IAuditReportService
{
    Task ExportAsync(
        AuditQuery query,
        ReportFormat format,
        string outputPath,
        CancellationToken cancellationToken);
}
