namespace Darhous.Archive.Core.Correlation;

/// <summary>
/// Ambient access to the <see cref="CorrelationId"/> of the operation currently in flight,
/// so deep call chains (Application → Event Bus → Job → Worker) don't need to thread it
/// through every method signature explicitly.
/// </summary>
public interface ICorrelationContextAccessor
{
    CorrelationId? Current { get; set; }
}
