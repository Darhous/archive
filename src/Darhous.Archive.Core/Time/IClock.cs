namespace Darhous.Archive.Core.Time;

/// <summary>
/// Implementation Plan §7 (Core Responsibilities: Clock) — every timestamp in the system
/// (documents, jobs, audit, sessions) goes through this instead of <c>DateTimeOffset.UtcNow</c>
/// directly, so tests can control time deterministically.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
