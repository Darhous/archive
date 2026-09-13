using Darhous.Archive.Configuration;
using Darhous.Archive.Security.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Darhous.Archive.Modules.Ai.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddAiModule_RegistersRegistryServicesAndHostedWorker()
    {
        var provider = new FakeAiProvider("registered-before-module");
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IAppSettingsStore, InMemoryAppSettingsStore>();
        builder.Services.AddSingleton<ISecretProtector, TestSecretProtector>();
        builder.Services.AddSingleton<IAiProvider>(provider);
        builder.Services.AddAiModule(new AiWorkerOptions
        {
            QueueCapacity = 2,
            ProviderTimeout = TimeSpan.FromSeconds(1),
        });

        using var host = builder.Build();
        await host.StartAsync();

        var registry = host.Services.GetRequiredService<IAiProviderRegistry>();
        Assert.True(registry.TryGetProvider(provider.ProviderId, out var resolved));
        Assert.Same(provider, resolved);
        Assert.IsType<AiSettingsService>(host.Services.GetRequiredService<IAiSettingsService>());
        Assert.IsType<AiSecretStore>(host.Services.GetRequiredService<IAiSecretStore>());
        Assert.IsType<AiAssistService>(host.Services.GetRequiredService<IAiAssistService>());
        Assert.IsType<AiWorker>(host.Services.GetRequiredService<IAiWorker>());

        await host.StopAsync();
    }

    private sealed class TestSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));

        public string Unprotect(string protectedValue) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue));
    }
}
