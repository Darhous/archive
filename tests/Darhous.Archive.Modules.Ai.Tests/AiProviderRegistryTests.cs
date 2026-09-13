namespace Darhous.Archive.Modules.Ai.Tests;

public class AiProviderRegistryTests
{
    [Fact]
    public void Register_ThenResolve_ReturnsProviderCaseInsensitively()
    {
        var provider = new FakeAiProvider("synthetic-provider");
        var registry = new AiProviderRegistry();

        Assert.True(registry.Register(provider));
        Assert.True(registry.TryGetProvider("SYNTHETIC-PROVIDER", out var resolved));
        Assert.Same(provider, resolved);
        Assert.Equal(["synthetic-provider"], registry.ProviderIds);
    }

    [Fact]
    public void UnknownProvider_ReturnsFalseWithoutThrowing()
    {
        var registry = new AiProviderRegistry();

        Assert.False(registry.TryGetProvider("missing", out var provider));
        Assert.Null(provider);
    }

    [Fact]
    public void DuplicateProviderId_DoesNotReplaceFirstProvider()
    {
        var first = new FakeAiProvider("duplicate");
        var second = new FakeAiProvider("DUPLICATE");
        var registry = new AiProviderRegistry([first]);

        Assert.False(registry.Register(second));
        Assert.True(registry.TryGetProvider("duplicate", out var resolved));
        Assert.Same(first, resolved);
    }
}
