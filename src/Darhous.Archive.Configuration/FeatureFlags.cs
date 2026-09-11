namespace Darhous.Archive.Configuration;

/// <summary>
/// Implementation Plan §114 — feature flags exist only for work-in-progress features and
/// must never become a permanent way to gate a shipped feature.
/// </summary>
public interface IFeatureFlagProvider
{
    bool IsEnabled(string flagName);
}
