namespace Darhous.Archive.Contracts.Jobs;

/// <summary>
/// Plugin SDK §59 (Job States).
/// </summary>
public enum JobStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Retrying,
    Cancelled,
    NeedsReview,
}
