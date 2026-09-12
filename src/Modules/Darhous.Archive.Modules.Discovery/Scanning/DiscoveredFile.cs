namespace Darhous.Archive.Modules.Discovery.Scanning;

/// <summary>Implementation Plan §52 (Discovery Safety) / §54 (Discovery Enumeration Rule) — path/extension/size/timestamps ONLY, no content read.</summary>
public sealed record DiscoveredFile(string FullPath, string Extension, long SizeBytes, DateTimeOffset? CreatedAtUtc, DateTimeOffset? ModifiedAtUtc);

public sealed record ScanResult(IReadOnlyList<DiscoveredFile> SupportedFiles, int FilesSeen, int SkippedByExclusion, int ErrorCount);
