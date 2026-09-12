using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Jobs;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>Same dual-mode pattern as <see cref="DocumentRepository"/> — reads (polling for ready-to-run jobs) don't need a transaction, writes (status transitions) run inside <see cref="IUnitOfWork"/>.</summary>
public sealed class JobRepository : IJobRepository
{
    private const string SelectColumns =
        """
        SELECT j.uid, j.job_type, j.owner_component, j.plugin_id, u.uid AS user_id, j.status,
               j.progress_percent, j.payload_json, j.result_json, j.error_code, j.error_message,
               j.retry_count, j.max_retries, j.priority, j.correlation_id,
               j.created_at, j.started_at, j.completed_at, j.next_retry_at
        FROM jobs j
        LEFT JOIN app_users u ON u.id = j.user_id
        """;

    private readonly ISqliteConnectionFactory? _connectionFactory;
    private readonly IDbConnection? _boundConnection;
    private readonly IDbTransaction? _boundTransaction;

    public JobRepository(ISqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public JobRepository(IDbConnection connection, IDbTransaction transaction)
    {
        _boundConnection = connection;
        _boundTransaction = transaction;
    }

    public async Task<Guid> CreateAsync(NewJob job, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(CreateAsync));

        var uid = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO jobs (uid, job_type, owner_component, plugin_id, user_id, status, payload_json, max_retries, priority, correlation_id, created_at)
            VALUES (@Uid, @JobType, @OwnerComponent, @PluginId, (SELECT id FROM app_users WHERE uid = @UserId),
                    'pending', @PayloadJson, @MaxRetries, @Priority, @CorrelationId, @Now);
            """,
            new
            {
                Uid = uid.ToString(), job.JobType, job.OwnerComponent, job.PluginId, UserId = job.UserId?.ToString(),
                job.PayloadJson, job.MaxRetries, job.Priority, CorrelationId = job.CorrelationId?.ToString(), Now = now,
            },
            _boundTransaction,
            cancellationToken: cancellationToken));

        return uid;
    }

    public Task<Job?> GetByUidAsync(Guid uid, CancellationToken cancellationToken) =>
        QuerySingleAsync($"{SelectColumns} WHERE j.uid = @Uid;", new { Uid = uid.ToString() }, cancellationToken);

    public async Task<IReadOnlyList<Job>> ListReadyToRunAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        var sql =
            $"""
            {SelectColumns}
            WHERE j.status IN ('pending', 'retrying') AND (j.next_retry_at IS NULL OR j.next_retry_at <= @Now)
            ORDER BY j.priority DESC, j.created_at ASC
            LIMIT @Limit;
            """;
        var parameters = new { Now = now.ToUnixTimeMilliseconds(), Limit = limit };

        return await QueryListAsync(sql, parameters, cancellationToken);
    }

    public Task<IReadOnlyList<Job>> ListByStatusAsync(JobStatus status, CancellationToken cancellationToken) =>
        QueryListAsync($"{SelectColumns} WHERE j.status = @Status ORDER BY j.created_at;", new { Status = ToDbString(status) }, cancellationToken);

    public Task MarkRunningAsync(Guid uid, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(MarkRunningAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE jobs SET status = 'running', started_at = @StartedAt WHERE uid = @Uid;",
            new { Uid = uid.ToString(), StartedAt = startedAt.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task ReportProgressAsync(Guid uid, int percent, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(ReportProgressAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE jobs SET progress_percent = @Percent WHERE uid = @Uid;",
            new { Uid = uid.ToString(), Percent = percent },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task MarkSucceededAsync(Guid uid, string? resultJson, DateTimeOffset completedAt, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(MarkSucceededAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE jobs SET status = 'succeeded', progress_percent = 100, result_json = @ResultJson, completed_at = @CompletedAt WHERE uid = @Uid;",
            new { Uid = uid.ToString(), ResultJson = resultJson, CompletedAt = completedAt.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task MarkFailedAsync(Guid uid, string errorCode, string errorMessage, DateTimeOffset completedAt, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(MarkFailedAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE jobs SET status = 'failed', error_code = @ErrorCode, error_message = @ErrorMessage, completed_at = @CompletedAt WHERE uid = @Uid;",
            new { Uid = uid.ToString(), ErrorCode = errorCode, ErrorMessage = errorMessage, CompletedAt = completedAt.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task ScheduleRetryAsync(Guid uid, int retryCount, DateTimeOffset nextRetryAt, string? errorCode, string? errorMessage, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(ScheduleRetryAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE jobs
            SET status = 'retrying', retry_count = @RetryCount, next_retry_at = @NextRetryAt,
                error_code = @ErrorCode, error_message = @ErrorMessage
            WHERE uid = @Uid;
            """,
            new
            {
                Uid = uid.ToString(), RetryCount = retryCount, NextRetryAt = nextRetryAt.ToUnixTimeMilliseconds(),
                ErrorCode = errorCode, ErrorMessage = errorMessage,
            },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task MarkNeedsReviewAsync(Guid uid, string errorCode, string errorMessage, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(MarkNeedsReviewAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE jobs SET status = 'needs_review', error_code = @ErrorCode, error_message = @ErrorMessage WHERE uid = @Uid;",
            new { Uid = uid.ToString(), ErrorCode = errorCode, ErrorMessage = errorMessage },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task RequeueAsync(Guid uid, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(RequeueAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE jobs SET status = 'pending', started_at = NULL WHERE uid = @Uid;",
            new { Uid = uid.ToString() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    private void RequireWriteMode(string memberName)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException($"{nameof(JobRepository)}.{memberName} must run inside IUnitOfWork.ExecuteAsync.");
        }
    }

    private async Task<Job?> QuerySingleAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        if (_boundConnection is not null)
        {
            var row = await _boundConnection.QuerySingleOrDefaultAsync<JobRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return row is null ? null : Map(row);
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRow = await connection.QuerySingleOrDefaultAsync<JobRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRow is null ? null : Map(readRow);
    }

    private async Task<IReadOnlyList<Job>> QueryListAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<JobRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<JobRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    private static string ToDbString(JobStatus status) => status switch
    {
        JobStatus.Pending => "pending",
        JobStatus.Running => "running",
        JobStatus.Succeeded => "succeeded",
        JobStatus.Failed => "failed",
        JobStatus.Retrying => "retrying",
        JobStatus.Cancelled => "cancelled",
        JobStatus.NeedsReview => "needs_review",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static JobStatus ParseStatus(string value) => value switch
    {
        "pending" => JobStatus.Pending,
        "running" => JobStatus.Running,
        "succeeded" => JobStatus.Succeeded,
        "failed" => JobStatus.Failed,
        "retrying" => JobStatus.Retrying,
        "cancelled" => JobStatus.Cancelled,
        "needs_review" => JobStatus.NeedsReview,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown job status."),
    };

    private static Job Map(JobRow row) => new(
        Guid.Parse(row.Uid), row.JobType, row.OwnerComponent, row.PluginId,
        row.UserId is null ? null : Guid.Parse(row.UserId), ParseStatus(row.Status), (int)row.ProgressPercent,
        row.PayloadJson, row.ResultJson, row.ErrorCode, row.ErrorMessage, row.RetryCount, row.MaxRetries, row.Priority,
        row.CorrelationId is null ? null : Guid.Parse(row.CorrelationId),
        DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAt),
        row.StartedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.StartedAt.Value) : null,
        row.CompletedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.CompletedAt.Value) : null,
        row.NextRetryAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.NextRetryAt.Value) : null);

    private sealed class JobRow
    {
        public string Uid { get; set; } = "";
        public string JobType { get; set; } = "";
        public string OwnerComponent { get; set; } = "";
        public string? PluginId { get; set; }
        public string? UserId { get; set; }
        public string Status { get; set; } = "";
        public double ProgressPercent { get; set; }
        public string? PayloadJson { get; set; }
        public string? ResultJson { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public int Priority { get; set; }
        public string? CorrelationId { get; set; }
        public long CreatedAt { get; set; }
        public long? StartedAt { get; set; }
        public long? CompletedAt { get; set; }
        public long? NextRetryAt { get; set; }
    }
}
