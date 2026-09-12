using Darhous.Archive.Modules.Discovery.Watching;

namespace Darhous.Archive.Modules.Discovery.Tests;

public class FileReadinessCheckerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"darhous-readiness-{Guid.NewGuid():N}.pdf");

    public void Dispose()
    {
        try { File.Delete(_path); } catch (IOException) { }
    }

    [Fact]
    public async Task WaitUntilReadyAsync_FileNotLocked_ReturnsTrueImmediately()
    {
        await File.WriteAllTextAsync(_path, "done writing");

        var ready = await FileReadinessChecker.WaitUntilReadyAsync(_path, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.True(ready);
    }

    [Fact]
    public async Task WaitUntilReadyAsync_FileDoesNotExist_ReturnsFalse()
    {
        var ready = await FileReadinessChecker.WaitUntilReadyAsync(_path, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.False(ready);
    }

    [Fact]
    public async Task WaitUntilReadyAsync_FileExclusivelyLocked_TimesOutFalse()
    {
        await File.WriteAllTextAsync(_path, "still writing");

        await using var lockingStream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None);

        var ready = await FileReadinessChecker.WaitUntilReadyAsync(_path, TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.False(ready);
    }

    [Fact]
    public async Task WaitUntilReadyAsync_LockReleasedBeforeTimeout_BecomesReady()
    {
        await File.WriteAllTextAsync(_path, "being copied");
        var lockingStream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None);

        var waitTask = FileReadinessChecker.WaitUntilReadyAsync(_path, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50), CancellationToken.None);
        await Task.Delay(300);
        await lockingStream.DisposeAsync();

        Assert.True(await waitTask);
    }
}
