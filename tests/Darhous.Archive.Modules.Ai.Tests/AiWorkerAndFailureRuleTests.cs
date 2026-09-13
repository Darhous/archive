using Darhous.Archive.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Modules.Ai.Tests;

public class AiWorkerAndFailureRuleTests
{
    [Fact]
    public async Task Worker_InvokesProviderOffCallingThread()
    {
        var callerThread = 0;
        var providerThread = 0;
        var provider = new FakeAiProvider("synthetic-provider")
        {
            Execute = (_, _) =>
            {
                providerThread = Environment.CurrentManagedThreadId;
                return Task.FromResult(new AiResponse("model", "done"));
            },
        };
        using var worker = CreateWorker(TimeSpan.FromSeconds(2));
        await worker.StartAsync(CancellationToken.None);

        var completed = new TaskCompletionSource<AiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callingThread = new Thread(() =>
        {
            try
            {
                callerThread = Environment.CurrentManagedThreadId;
                completed.TrySetResult(worker.ExecuteAsync(
                    provider,
                    new AiRequest("model", "prompt"),
                    CancellationToken.None).GetAwaiter().GetResult());
            }
            catch (Exception exception)
            {
                completed.TrySetException(exception);
            }
        });
        callingThread.Start();
        var response = await completed.Task;
        callingThread.Join();

        Assert.Equal("done", response.Content);
        Assert.NotEqual(callerThread, providerThread);
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ThrowingProvider_ReturnsNoEnrichment_AndCallerContinues()
    {
        var provider = new FakeAiProvider("synthetic-provider")
        {
            Execute = (_, _) => throw new InvalidOperationException("provider is down"),
        };
        var (service, worker) = await CreateEnabledServiceAsync(provider, TimeSpan.FromSeconds(2));
        using (worker)
        {
            var documentSaveCompleted = true;

            var result = await service.TryExecuteAsync(
                new AiRequest("model", "optional enrichment"),
                CancellationToken.None);

            Assert.True(documentSaveCompleted);
            Assert.False(result.Enriched);
            Assert.Equal("provider_failure", result.FailureCode);
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TimedOutProvider_ReturnsNoEnrichment_InsteadOfPropagating()
    {
        var provider = new FakeAiProvider("synthetic-provider")
        {
            Execute = async (_, _) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None);
                return new AiResponse("model", "unreachable");
            },
        };
        var (service, worker) = await CreateEnabledServiceAsync(provider, TimeSpan.FromMilliseconds(50));
        using (worker)
        {
            var result = await service.TryExecuteAsync(
                new AiRequest("model", "optional enrichment"),
                CancellationToken.None);

            Assert.False(result.Enriched);
            Assert.Equal("provider_failure", result.FailureCode);
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task SettingsStoreFailure_ReturnsNoEnrichment_InsteadOfPropagating()
    {
        var provider = new FakeAiProvider("synthetic-provider");
        var registry = new AiProviderRegistry([provider]);
        var settings = new ThrowingSettingsService();
        using var worker = CreateWorker(TimeSpan.FromSeconds(2));
        await worker.StartAsync(CancellationToken.None);
        var service = new AiAssistService(
            settings,
            registry,
            worker,
            NullLogger<AiAssistService>.Instance);

        var result = await service.TryExecuteAsync(
            new AiRequest("model", "optional enrichment"),
            CancellationToken.None);

        Assert.False(result.Enriched);
        Assert.Equal("provider_failure", result.FailureCode);
        await worker.StopAsync(CancellationToken.None);
    }

    private static AiWorker CreateWorker(TimeSpan timeout) =>
        new(
            new AiWorkerOptions { QueueCapacity = 4, ProviderTimeout = timeout },
            NullLogger<AiWorker>.Instance);

    private static async Task<(AiAssistService Service, AiWorker Worker)> CreateEnabledServiceAsync(
        FakeAiProvider provider,
        TimeSpan timeout)
    {
        var registry = new AiProviderRegistry([provider]);
        var settings = new AiSettingsService(
            new InMemoryAppSettingsStore(),
            registry,
            NullLogger<AiSettingsService>.Instance);
        await settings.SetPrivacyAcknowledgedAsync(true, CancellationToken.None);
        await settings.SetProviderEnabledAsync(provider.ProviderId, true, CancellationToken.None);
        await settings.SetActiveProviderAsync(provider.ProviderId, CancellationToken.None);

        var worker = CreateWorker(timeout);
        await worker.StartAsync(CancellationToken.None);
        var service = new AiAssistService(
            settings,
            registry,
            worker,
            NullLogger<AiAssistService>.Instance);
        return (service, worker);
    }

    private sealed class ThrowingSettingsService : IAiSettingsService
    {
        public Task<AiSettings> GetAsync(CancellationToken cancellationToken) =>
            throw new IOException("settings database unavailable");

        public Task SetPrivacyAcknowledgedAsync(bool acknowledged, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetProviderEnabledAsync(string providerId, bool enabled, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetActiveProviderAsync(string? providerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
