namespace Darhous.Archive.Contracts.Audit;

/// <summary>
/// What a caller provides to log one audit event. <see cref="IAuditService"/> (Core) fills in
/// uid/timestamp/machine_name and decides Critical vs Buffered delivery — see
/// DB Spec §82 (audit_events) and §106 (Audit Critical Events).
/// </summary>
public sealed record AuditEntry(
    string Action,
    AuditActionCategory Category,
    AuditResult Result,
    Guid? UserId = null,
    string? UsernameSnapshot = null,
    string? RoleSnapshot = null,
    string? EntityType = null,
    string? EntityUid = null,
    string? EntityNameSnapshot = null,
    string? Details = null,
    string? SearchQuery = null,
    string? SortExpression = null,
    string? FilterExpression = null,
    string? BeforeJson = null,
    string? AfterJson = null,
    string? ErrorCode = null,
    Guid? CorrelationId = null,
    string? JobUid = null,
    string? PluginId = null);
