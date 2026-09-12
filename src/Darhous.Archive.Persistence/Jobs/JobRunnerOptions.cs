namespace Darhous.Archive.Persistence.Jobs;

public sealed class JobRunnerOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Jobs run one at a time on the single runner loop (V1 simplification — no concurrent job execution yet; nothing in the current job types needs it).</summary>
    public int BatchSize { get; set; } = 1;

    /// <summary>Base delay for retry backoff: attempt N waits BaseRetryDelay * 2^(N-1), capped at MaxRetryDelay.</summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(30);
}
