using System.Globalization;
using Darhous.Archive.Contracts.Audit;
using Darhous.Archive.Core.Audit;
using Darhous.Archive.Modules.Reports.Exporting;

namespace Darhous.Archive.Modules.Reports.Audit;

public sealed class AuditReportService(
    IAuditQueryService auditQueryService,
    IReportExportService exportService) : IAuditReportService
{
    private static readonly string[] Columns =
    [
        "Uid", "Timestamp", "UserId", "Username", "Role", "Action", "Category", "EntityType",
        "EntityUid", "EntityName", "Details", "SearchQuery", "SortExpression", "FilterExpression",
        "BeforeJson", "AfterJson", "Result", "ErrorCode", "CorrelationId", "JobUid", "PluginId",
        "MachineName",
    ];

    public async Task ExportAsync(
        AuditQuery query,
        ReportFormat format,
        string outputPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var events = await auditQueryService.QueryAsync(query, cancellationToken);
        var subtitle = $"Skip={query.Skip}; Take={query.Take}; Exported rows={events.Count}";
        var report = new ReportDefinition(
            "Audit events",
            Columns,
            events.Select(ToRow),
            subtitle);

        await exportService.ExportToFileAsync(report, format, outputPath, cancellationToken);
    }

    private static IEnumerable<string?> ToRow(AuditEvent auditEvent)
    {
        return
        [
            auditEvent.Uid.ToString(),
            auditEvent.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            auditEvent.UserId?.ToString(),
            auditEvent.UsernameSnapshot,
            auditEvent.RoleSnapshot,
            auditEvent.Action,
            auditEvent.Category.ToString(),
            auditEvent.EntityType,
            auditEvent.EntityUid,
            auditEvent.EntityNameSnapshot,
            auditEvent.Details,
            auditEvent.SearchQuery,
            auditEvent.SortExpression,
            auditEvent.FilterExpression,
            auditEvent.BeforeJson,
            auditEvent.AfterJson,
            auditEvent.Result.ToString(),
            auditEvent.ErrorCode,
            auditEvent.CorrelationId?.ToString(),
            auditEvent.JobUid,
            auditEvent.PluginId,
            auditEvent.MachineName,
        ];
    }
}
