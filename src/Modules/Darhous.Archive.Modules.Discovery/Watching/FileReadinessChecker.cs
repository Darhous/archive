namespace Darhous.Archive.Modules.Discovery.Watching;

/// <summary>
/// Implementation Plan §49 (File Watcher): "Wait until write complete" — a newly-created file
/// can still be mid-copy when <c>Created</c> fires. Polls for an exclusive open (no other
/// process still writing) rather than a fixed sleep, so a small file isn't delayed
/// unnecessarily and a large one isn't grabbed too early.
/// </summary>
public static class FileReadinessChecker
{
    public static async Task<bool> WaitUntilReadyAsync(string path, TimeSpan timeout, TimeSpan pollInterval, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(path))
            {
                return false;
            }

            if (IsReady(path))
            {
                return true;
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        return false;
    }

    private static bool IsReady(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
