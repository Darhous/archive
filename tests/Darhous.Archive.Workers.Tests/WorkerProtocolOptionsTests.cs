namespace Darhous.Archive.Workers.Tests;

public sealed class WorkerProtocolOptionsTests
{
    [Fact]
    public void AllOperationalValuesCanBeOverriddenForAnIsolatedTest()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var options = new WorkerProtocolOptions
        {
            PipeNamePrefix = "Darhous.Tests.Worker",
            ProtocolVersion = 17,
            HandshakeTimeout = TimeSpan.FromMilliseconds(750),
            ManagedTempStorageRoot = root,
            MaximumFrameLengthBytes = 4096,
        };

        Assert.Equal("Darhous.Tests.Worker", options.PipeNamePrefix);
        Assert.Equal(17, options.ProtocolVersion);
        Assert.Equal(TimeSpan.FromMilliseconds(750), options.HandshakeTimeout);
        Assert.Equal(root, options.ManagedTempStorageRoot);
        Assert.Equal(4096, options.MaximumFrameLengthBytes);
    }
}
