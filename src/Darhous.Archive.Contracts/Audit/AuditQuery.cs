namespace Darhous.Archive.Contracts.Audit;

/// <summary>SAD §15 (Audit Log) — Admin can filter by user, action type, and date; paginated.</summary>
public sealed record AuditQuery(
    Guid? UserId = null,
    string? Action = null,
    string? EntityUid = null,
    DateTimeOffset? FromTimestamp = null,
    DateTimeOffset? ToTimestamp = null,
    int Skip = 0,
    int Take = 100);
