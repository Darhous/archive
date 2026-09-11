namespace Darhous.Archive.Core.Correlation;

/// <summary>
/// Default <see cref="ICorrelationContextAccessor"/> — flows the correlation id across
/// async/await continuations within one logical operation via <see cref="AsyncLocal{T}"/>.
/// </summary>
public sealed class AsyncLocalCorrelationContextAccessor : ICorrelationContextAccessor
{
    private static readonly AsyncLocal<CorrelationId?> Holder = new();

    public CorrelationId? Current
    {
        get => Holder.Value;
        set => Holder.Value = value;
    }
}
