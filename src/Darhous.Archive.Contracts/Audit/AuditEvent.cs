namespace Darhous.Archive.Contracts.Audit;

/// <summary>The full persisted row (DB Spec §82) — what queries against audit.db return.</summary>
public sealed record AuditEvent(
    Guid Uid,
    DateTimeOffset Timestamp,
    Guid? UserId,
    string? UsernameSnapshot,
    string? RoleSnapshot,
    string Action,
    AuditActionCategory Category,
    string? EntityType,
    string? EntityUid,
    string? EntityNameSnapshot,
    string? Details,
    string? SearchQuery,
    string? SortExpression,
    string? FilterExpression,
    string? BeforeJson,
    string? AfterJson,
    AuditResult Result,
    string? ErrorCode,
    Guid? CorrelationId,
    string? JobUid,
    string? PluginId,
    string MachineName);
