namespace Darhous.Archive.Modules.Ai;

/// <summary>Provider-neutral AI contract from master documentation §81.</summary>
public interface IAiProvider
{
    string ProviderId { get; }

    Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(
        CancellationToken cancellationToken);

    Task<AiResponse> ExecuteAsync(
        AiRequest request,
        CancellationToken cancellationToken);
}
