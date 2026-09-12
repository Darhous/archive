namespace Darhous.Archive.PluginSdk;

/// <summary>Plugin SDK §14 — the official state values, verbatim.</summary>
public enum PluginLifecycleState
{
    NotInstalled,
    Installed,
    Disabled,
    Starting,
    Healthy,
    Degraded,
    Failed,
    Stopping,
    PendingRestart,
    UpdateAvailable,
    Incompatible,
    Quarantined,
}
