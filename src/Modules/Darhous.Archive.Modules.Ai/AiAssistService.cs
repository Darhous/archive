using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Ai;

public interface IAiAssistService
{
    Task<AiAssistResult> TryExecuteAsync(AiRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Safe integration seam for future document enrichment. Provider failures become a no-op
/// result; only cancellation explicitly requested by the caller is allowed to propagate.
/// This service is deliberately not wired into DocumentService in the abstraction-only phase.
/// </summary>
public sealed class AiAssistService(
    IAiSettingsService settingsService,
    IAiProviderRegistry providerRegistry,
    IAiWorker worker,
    ILogger<AiAssistService> logger) : IAiAssistService
{
    public async Task<AiAssistResult> TryExecuteAsync(
        AiRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? providerId = null;

        try
        {
            var settings = await settingsService.GetAsync(cancellationToken);
            if (!settings.PrivacyAcknowledged)
            {
                return AiAssistResult.NoEnrichment("privacy_not_acknowledged");
            }

            if (string.IsNullOrWhiteSpace(settings.ActiveProviderId))
            {
                return AiAssistResult.NoEnrichment("no_active_provider");
            }

            providerId = settings.ActiveProviderId;
            if (!settings.IsProviderEnabled(providerId))
            {
                return AiAssistResult.NoEnrichment("provider_disabled", providerId);
            }

            if (!providerRegistry.TryGetProvider(providerId, out var provider) || provider is null)
            {
                return AiAssistResult.NoEnrichment("provider_unavailable", providerId);
            }

            var response = await worker.ExecuteAsync(provider, request, cancellationToken);
            return AiAssistResult.Succeeded(response.Content, providerId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Optional AI enrichment failed for provider {ProviderId}", providerId ?? "unknown");
            return AiAssistResult.NoEnrichment("provider_failure", providerId);
        }
    }
}
