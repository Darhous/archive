namespace Darhous.Archive.Modules.Updates.Packaging;

public sealed record UpdatePackageManifest(
    int SchemaVersion,
    string ComponentType,
    string ComponentId,
    string Version,
    bool IncludesMigration);

internal sealed record ValidatedUpdatePackage(
    UpdatePackageManifest Manifest,
    Version Version,
    IReadOnlyList<string> PayloadFiles,
    string PackageSha256);
