using System.Text.Json;

namespace Darhous.Archive.Modules.Updates.Sources;

/// <summary>
/// Offline-capable Phase 19 source. A future Velopack/CDN adapter can implement
/// <see cref="IUpdateSource"/> without changing the update safety/apply pipeline.
/// </summary>
public sealed class LocalManifestUpdateSource(UpdateModuleOptions options) : IUpdateSource
{
    public async Task<UpdateCandidate?> GetLatestAsync(
        string componentType,
        string componentId,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(options.FeedManifestPath))
        {
            return null;
        }

        await using var stream = new FileStream(
            options.FeedManifestPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var feed = await JsonSerializer.DeserializeAsync(
            stream, UpdateJsonContext.Default.UpdateFeedManifest, cancellationToken)
            ?? throw new InvalidDataException("Update feed manifest is empty.");
        if (feed.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported update feed schema version {feed.SchemaVersion}.");
        }

        var feedDirectory = Path.GetDirectoryName(Path.GetFullPath(options.FeedManifestPath))!;
        return feed.Updates
            .Where(item => string.Equals(item.ComponentType, componentType, StringComparison.OrdinalIgnoreCase) &&
                           string.Equals(item.ComponentId, componentId, StringComparison.OrdinalIgnoreCase))
            .Select(item => new UpdateCandidate(
                item.ComponentType,
                item.ComponentId,
                ParseVersion(item.Version),
                ResolvePackagePath(feedDirectory, item.PackagePath),
                item.IncludesMigration))
            .OrderByDescending(item => item.Version)
            .FirstOrDefault();
    }

    private static string ResolvePackagePath(string feedDirectory, string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        return Path.GetFullPath(Path.IsPathRooted(packagePath)
            ? packagePath
            : Path.Combine(feedDirectory, packagePath));
    }

    private static Version ParseVersion(string value) =>
        Version.TryParse(value, out var version)
            ? version
            : throw new InvalidDataException($"Update feed version '{value}' is invalid.");
}

public sealed record UpdateFeedManifest(int SchemaVersion, IReadOnlyList<UpdateFeedItem> Updates);

public sealed record UpdateFeedItem(
    string ComponentType,
    string ComponentId,
    string Version,
    string PackagePath,
    bool IncludesMigration);
