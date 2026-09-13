using System.Text.Json.Serialization;

namespace Darhous.Backup.Local.Packaging;

internal sealed record BackupPackageManifest(
    int FormatVersion,
    Guid BackupUid,
    string BackupType,
    string ApplicationVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, long> SchemaVersions,
    IReadOnlyList<BackupPackageEntry> Entries);

internal sealed record BackupPackageEntry(string Path, long Size, string Sha256);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(BackupPackageManifest))]
internal partial class BackupJsonContext : JsonSerializerContext;
