namespace Darhous.Archive.Modules.Discovery.Jobs;

public sealed record DiscoveryScanJobPayload(
    Guid DiscoveryRunUid,
    string RootPath,
    bool IncludeSubfolders,
    string ImportMode,
    Guid? DestinationFolderId,
    string SourceType,
    Guid? RequestedBy);

public sealed record FileIndexJobPayload(
    string FilePath,
    string ImportMode,
    Guid? DestinationFolderId,
    string SourceType,
    Guid? RequestedBy);
