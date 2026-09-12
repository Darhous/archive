using Darhous.Archive.Modules.Plugins.Versioning;
using Darhous.Archive.PluginSdk;

namespace Darhous.Archive.Modules.Plugins.Lifecycle;

/// <summary>Plugin SDK §43-44 — checked immediately before Enable, not at install time (a plugin can be installed-but-disabled with unmet dependencies; that only actually blocks turning it on).</summary>
public static class PluginDependencyResolver
{
    public sealed record DependencySnapshot(string PluginId, string ActiveVersion, bool IsEnabled, bool IsFailed);

    public static IReadOnlyList<string> ValidateForEnable(PluginManifest manifest, IReadOnlyList<DependencySnapshot> installedPlugins)
    {
        var errors = new List<string>();
        var byId = installedPlugins.ToDictionary(p => p.PluginId);

        foreach (var dependency in manifest.Dependencies ?? [])
        {
            if (!byId.TryGetValue(dependency.Id, out var installed))
            {
                errors.Add($"Missing dependency '{dependency.Id}'.");
                continue;
            }

            if (installed.IsFailed)
            {
                errors.Add($"Dependency '{dependency.Id}' is in a Failed state.");
                continue;
            }

            if (!installed.IsEnabled)
            {
                errors.Add($"Dependency '{dependency.Id}' is Disabled.");
                continue;
            }

            if (!SemVer.TryParse(installed.ActiveVersion, out var installedVersion) || !SemVerRange.IsSatisfiedBy(dependency.Version, installedVersion))
            {
                errors.Add($"Dependency '{dependency.Id}' version {installed.ActiveVersion} does not satisfy required range '{dependency.Version}'.");
            }
        }

        return errors;
    }

    /// <summary>Plugin SDK §44 — A→B→A (or longer cycles) are rejected outright, checked across the whole installed set, not just this one manifest's direct dependencies.</summary>
    public static bool HasCircularDependency(string pluginId, IReadOnlyDictionary<string, IReadOnlyList<string>> dependencyGraph)
    {
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();

        bool Visit(string id)
        {
            if (visited.Contains(id))
            {
                return false;
            }

            if (!visiting.Add(id))
            {
                return true; // back-edge found — cycle
            }

            foreach (var dependencyId in dependencyGraph.GetValueOrDefault(id, []))
            {
                if (Visit(dependencyId))
                {
                    return true;
                }
            }

            visiting.Remove(id);
            visited.Add(id);
            return false;
        }

        return Visit(pluginId);
    }
}
