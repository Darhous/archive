using System.Data;
using Dapper;
using Darhous.Archive.Contracts.Audit;
using Darhous.Archive.Core.Audit;

namespace Darhous.Archive.Audit.Writing;

/// <summary>Raw Dapper access to `audit_events`. Internal — everything outside this project talks to <see cref="IAuditService"/>.</summary>
internal static class AuditEventRepository
{
    public static async Task InsertBatchAsync(
        IDbConnection connection, IDbTransaction transaction, IReadOnlyCollection<PendingAuditEvent> events,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO audit_events
                (uid, timestamp, user_id, username_snapshot, role_snapshot, action, action_category,
                 entity_type, entity_uid, entity_name_snapshot, details, search_query, sort_expression,
                 filter_expression, before_json, after_json, result, error_code, correlation_id,
                 job_uid, plugin_id, machine_name, created_at)
            VALUES
                (@Uid, @Timestamp, @UserId, @UsernameSnapshot, @RoleSnapshot, @Action, @ActionCategory,
                 @EntityType, @EntityUid, @EntityNameSnapshot, @Details, @SearchQuery, @SortExpression,
                 @FilterExpression, @BeforeJson, @AfterJson, @Result, @ErrorCode, @CorrelationId,
                 @JobUid, @PluginId, @MachineName, @CreatedAt);
            """;

        var rows = events.Select(e => new
        {
            Uid = e.Uid.ToString(),
            Timestamp = e.Timestamp.ToUnixTimeMilliseconds(),
            UserId = (long?)null, // TODO(Phase 3 debt carried forward): see OutboxWriter — no
                                   // Guid->internal-id lookup available yet; audit still keeps
                                   // full identity via UsernameSnapshot/RoleSnapshot below.
            e.Entry.UsernameSnapshot,
            e.Entry.RoleSnapshot,
            e.Entry.Action,
            ActionCategory = e.Entry.Category.ToString(),
            e.Entry.EntityType,
            e.Entry.EntityUid,
            e.Entry.EntityNameSnapshot,
            e.Entry.Details,
            e.Entry.SearchQuery,
            e.Entry.SortExpression,
            e.Entry.FilterExpression,
            e.Entry.BeforeJson,
            e.Entry.AfterJson,
            Result = e.Entry.Result == AuditResult.Success ? "success" : "failure",
            e.Entry.ErrorCode,
            CorrelationId = e.Entry.CorrelationId?.ToString(),
            e.Entry.JobUid,
            e.Entry.PluginId,
            MachineName = Environment.MachineName,
            CreatedAt = e.Timestamp.ToUnixTimeMilliseconds(),
        });

        await connection.ExecuteAsync(new CommandDefinition(sql, rows, transaction, cancellationToken: cancellationToken));
    }

    public static async Task<IReadOnlyList<AuditEvent>> QueryAsync(
        IDbConnection connection, AuditQuery query, CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (query.UserId is { } userId)
        {
            // user_id is not yet populated (see InsertBatchAsync TODO) — filtering by it would
            // silently return nothing until that lookup exists, so this is intentionally unused
            // for now. Kept here so the AuditQuery shape doesn't need to change later.
            _ = userId;
        }

        if (query.Action is { } action)
        {
            conditions.Add("action = @Action");
            parameters.Add("Action", action);
        }

        if (query.EntityUid is { } entityUid)
        {
            conditions.Add("entity_uid = @EntityUid");
            parameters.Add("EntityUid", entityUid);
        }

        if (query.FromTimestamp is { } from)
        {
            conditions.Add("timestamp >= @From");
            parameters.Add("From", from.ToUnixTimeMilliseconds());
        }

        if (query.ToTimestamp is { } to)
        {
            conditions.Add("timestamp <= @To");
            parameters.Add("To", to.ToUnixTimeMilliseconds());
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        parameters.Add("Skip", query.Skip);
        parameters.Add("Take", query.Take);

        var sql =
            $"""
            SELECT uid, timestamp, user_id, username_snapshot, role_snapshot, action, action_category,
                   entity_type, entity_uid, entity_name_snapshot, details, search_query, sort_expression,
                   filter_expression, before_json, after_json, result, error_code, correlation_id,
                   job_uid, plugin_id, machine_name
            FROM audit_events
            {where}
            ORDER BY timestamp DESC
            LIMIT @Take OFFSET @Skip;
            """;

        var rows = await connection.QueryAsync<AuditEventRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.Select(Map).ToList();
    }

    private static AuditEvent Map(AuditEventRow row) => new(
        Guid.Parse(row.Uid),
        DateTimeOffset.FromUnixTimeMilliseconds(row.Timestamp),
        row.UserId is null ? null : Guid.Parse(row.UserId), // reserved for when user_id resolution lands
        row.UsernameSnapshot, row.RoleSnapshot, row.Action,
        Enum.Parse<AuditActionCategory>(row.ActionCategory),
        row.EntityType, row.EntityUid, row.EntityNameSnapshot, row.Details,
        row.SearchQuery, row.SortExpression, row.FilterExpression, row.BeforeJson, row.AfterJson,
        row.Result == "success" ? AuditResult.Success : AuditResult.Failure,
        row.ErrorCode, row.CorrelationId is null ? null : Guid.Parse(row.CorrelationId),
        row.JobUid, row.PluginId, row.MachineName);

    private sealed class AuditEventRow
    {
        public string Uid { get; set; } = "";
        public long Timestamp { get; set; }
        public string? UserId { get; set; }
        public string? UsernameSnapshot { get; set; }
        public string? RoleSnapshot { get; set; }
        public string Action { get; set; } = "";
        public string ActionCategory { get; set; } = "";
        public string? EntityType { get; set; }
        public string? EntityUid { get; set; }
        public string? EntityNameSnapshot { get; set; }
        public string? Details { get; set; }
        public string? SearchQuery { get; set; }
        public string? SortExpression { get; set; }
        public string? FilterExpression { get; set; }
        public string? BeforeJson { get; set; }
        public string? AfterJson { get; set; }
        public string Result { get; set; } = "";
        public string? ErrorCode { get; set; }
        public string? CorrelationId { get; set; }
        public string? JobUid { get; set; }
        public string? PluginId { get; set; }
        public string MachineName { get; set; } = "";
    }
}

internal sealed record PendingAuditEvent(Guid Uid, DateTimeOffset Timestamp, AuditEntry Entry);
