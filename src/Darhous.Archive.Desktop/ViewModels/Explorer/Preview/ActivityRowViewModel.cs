using Darhous.Archive.Contracts.Audit;

namespace Darhous.Archive.Desktop.ViewModels.Explorer.Preview;

/// <summary>One row in the Preview pane's Activity tab (SAD §21) — a single audit_events row scoped to this document.</summary>
public sealed class ActivityRowViewModel(AuditEvent auditEvent)
{
    public string Timestamp { get; } = auditEvent.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string Action { get; } = auditEvent.Action;
    public string? UserDisplay { get; } = auditEvent.UsernameSnapshot;
    public string ResultDisplay { get; } = auditEvent.Result == AuditResult.Success ? "نجاح" : "فشل";
}
