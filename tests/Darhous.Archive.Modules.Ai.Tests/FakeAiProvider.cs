namespace Darhous.Archive.Modules.Ai.Tests;

internal sealed class FakeAiProvider(string providerId) : IAiProvider
{
    public string ProviderId { get; } = providerId;

    public Func<CancellationToken, Task<IReadOnlyList<AiModelInfo>>> ListModels { get; init; } =
        _ => Task.FromResult<IReadOnlyList<AiModelInfo>>([new AiModelInfo("model", "Synthetic Model")]);

    public Func<AiRequest, CancellationToken, Task<AiResponse>> Execute { get; init; } =
        (request, _) => Task.FromResult(new AiResponse(request.ModelId, "synthetic response"));

    public Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(CancellationToken cancellationToken) =>
        ListModels(cancellationToken);

    public Task<AiResponse> ExecuteAsync(AiRequest request, CancellationToken cancellationToken) =>
        Execute(request, cancellationToken);
}
