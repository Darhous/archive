using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>Write-only-in-practice — a run is created and updated entirely from within one DiscoveryScanJob execution.</summary>
public sealed class DiscoveryRunRepository(IDbConnection connection, IDbTransaction transaction) : IDiscoveryRunRepository
{
    public async Task<Guid> StartAsync(string runType, Guid? requestedBy, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var uid = Guid.CreateVersion7();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO discovery_runs (uid, run_type, status, requested_by, started_at)
            VALUES (@Uid, @RunType, 'running', (SELECT id FROM app_users WHERE uid = @RequestedBy), @StartedAt);
            """,
            new { Uid = uid.ToString(), RunType = runType, RequestedBy = requestedBy?.ToString(), StartedAt = startedAt.ToUnixTimeMilliseconds() },
            transaction,
            cancellationToken: cancellationToken));

        return uid;
    }

    public async Task<DiscoveryRun?> GetByUidAsync(Guid uid, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT uid, run_type, status, started_at, completed_at, drives_scanned, files_seen,
                   supported_files, queued_for_index, skipped_by_exclusion, missing_detected, error_count
            FROM discovery_runs WHERE uid = @Uid;
            """;

        var row = await connection.QuerySingleOrDefaultAsync<RunRow>(
            new CommandDefinition(sql, new { Uid = uid.ToString() }, transaction, cancellationToken: cancellationToken));

        return row is null ? null : Map(row);
    }

    public Task IncrementCountersAsync(
        Guid uid, int filesSeenDelta, int supportedFilesDelta, int queuedForIndexDelta,
        int skippedByExclusionDelta, int missingDetectedDelta, int errorCountDelta, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE discovery_runs
            SET files_seen = files_seen + @FilesSeenDelta,
                supported_files = supported_files + @SupportedFilesDelta,
                queued_for_index = queued_for_index + @QueuedForIndexDelta,
                skipped_by_exclusion = skipped_by_exclusion + @SkippedByExclusionDelta,
                missing_detected = missing_detected + @MissingDetectedDelta,
                error_count = error_count + @ErrorCountDelta
            WHERE uid = @Uid;
            """,
            new
            {
                Uid = uid.ToString(), FilesSeenDelta = filesSeenDelta, SupportedFilesDelta = supportedFilesDelta,
                QueuedForIndexDelta = queuedForIndexDelta, SkippedByExclusionDelta = skippedByExclusionDelta,
                MissingDetectedDelta = missingDetectedDelta, ErrorCountDelta = errorCountDelta,
            },
            transaction,
            cancellationToken: cancellationToken));

    public Task CompleteAsync(Guid uid, string status, int drivesScanned, DateTimeOffset completedAt, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            "UPDATE discovery_runs SET status = @Status, drives_scanned = @DrivesScanned, completed_at = @CompletedAt WHERE uid = @Uid;",
            new { Uid = uid.ToString(), Status = status, DrivesScanned = drivesScanned, CompletedAt = completedAt.ToUnixTimeMilliseconds() },
            transaction,
            cancellationToken: cancellationToken));

    private static DiscoveryRun Map(RunRow row) => new(
        Guid.Parse(row.Uid), row.RunType, row.Status, DateTimeOffset.FromUnixTimeMilliseconds(row.StartedAt),
        row.CompletedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.CompletedAt.Value) : null,
        row.DrivesScanned, row.FilesSeen, row.SupportedFiles, row.QueuedForIndex, row.SkippedByExclusion, row.MissingDetected, row.ErrorCount);

    private sealed class RunRow
    {
        public string Uid { get; set; } = "";
        public string RunType { get; set; } = "";
        public string Status { get; set; } = "";
        public long StartedAt { get; set; }
        public long? CompletedAt { get; set; }
        public int DrivesScanned { get; set; }
        public int FilesSeen { get; set; }
        public int SupportedFiles { get; set; }
        public int QueuedForIndex { get; set; }
        public int SkippedByExclusion { get; set; }
        public int MissingDetected { get; set; }
        public int ErrorCount { get; set; }
    }
}
