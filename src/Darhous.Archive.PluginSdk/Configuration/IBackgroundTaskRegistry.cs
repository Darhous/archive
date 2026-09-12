namespace Darhous.Archive.PluginSdk.Configuration;

/// <summary>Plugin SDK §19 — the ONLY sanctioned way for a plugin to run recurring work; a plugin must never spin up its own unmanaged timer/thread. The host owns start/stop of every registered task, tied to the plugin's own lifecycle.</summary>
public interface IBackgroundTaskRegistry
{
    void RegisterPeriodic(string taskId, TimeSpan interval, Func<CancellationToken, Task> action);
}
