using Darhous.Archive.PluginSdk.Configuration;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Plugins.Hosting;

/// <summary>Owns start/stop of every task a plugin registers — a plugin can never run its own unmanaged timer/thread (Plugin SDK §19).</summary>
public sealed class BackgroundTaskRegistry(ILogger logger) : IBackgroundTaskRegistry, IAsyncDisposable
{
    private readonly List<(string TaskId, TimeSpan Interval, Func<CancellationToken, Task> Action)> _registrations = [];
    private readonly List<(PeriodicTimer Timer, Task Loop)> _running = [];
    private CancellationTokenSource? _stopSource;

    public void RegisterPeriodic(string taskId, TimeSpan interval, Func<CancellationToken, Task> action) =>
        _registrations.Add((taskId, interval, action));

    public void StartAll()
    {
        _stopSource = new CancellationTokenSource();
        foreach (var (taskId, interval, action) in _registrations)
        {
            var timer = new PeriodicTimer(interval);
            var loop = RunLoopAsync(taskId, timer, action, _stopSource.Token);
            _running.Add((timer, loop));
        }
    }

    private async Task RunLoopAsync(string taskId, PeriodicTimer timer, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await action(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Plugin background task '{TaskId}' failed", taskId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on stop.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stopSource?.Cancel();
        foreach (var (timer, loop) in _running)
        {
            timer.Dispose();
            try { await loop; } catch (OperationCanceledException) { }
        }

        _running.Clear();
    }
}
