using Darhous.Archive.Configuration;
using Darhous.Archive.Security.Secrets;

namespace Darhous.Archive.Modules.Ai.Tests;

public class AiSecretStoreTests
{
    [Fact]
    public async Task SyntheticProviderKey_RoundTripsThroughDpapi_WithoutPlaintextAtRest()
    {
        var settings = new RecordingSettingsStore();
        var secrets = new AiSecretStore(settings, new DpapiSecretProtector());
        const string apiKey = "synthetic-placeholder-key-never-sent";

        await secrets.SetApiKeyAsync("synthetic-provider", apiKey, CancellationToken.None);

        Assert.NotNull(settings.LastWrittenValue);
        Assert.DoesNotContain(apiKey, settings.LastWrittenValue, StringComparison.Ordinal);
        Assert.Equal(apiKey, await secrets.GetApiKeyAsync("synthetic-provider", CancellationToken.None));

        await secrets.ClearApiKeyAsync("synthetic-provider", CancellationToken.None);
        Assert.Null(await secrets.GetApiKeyAsync("synthetic-provider", CancellationToken.None));
    }

    private sealed class RecordingSettingsStore : IAppSettingsStore
    {
        private readonly Dictionary<string, string> _values = [];

        public string? LastWrittenValue { get; private set; }

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(_values.GetValueOrDefault(key));

        public Task SetAsync(string key, string value, CancellationToken cancellationToken)
        {
            LastWrittenValue = value;
            _values[key] = value;
            return Task.CompletedTask;
        }
    }
}
