using Darhous.Archive.Core.Hosting;
using Microsoft.Extensions.Hosting;

namespace Darhous.Archive.Core.Tests;

public class ArchiveHostDefaultsTests
{
    [Fact]
    public async Task CreateBuilder_BuildsAndStartsHost_WithSerilogWired()
    {
        var builder = ArchiveHostDefaults.CreateBuilder(nameof(ArchiveHostDefaultsTests));

        using var host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public void CreateBuilder_Throws_WhenComponentNameMissing()
    {
        Assert.Throws<ArgumentException>(() => ArchiveHostDefaults.CreateBuilder(string.Empty));
    }
}
