using System.IO.Compression;
using System.Text.Json;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Backup.Local.Packaging;

namespace Darhous.Backup.Local.Tests;

public sealed class BackupFlowTests : LocalBackupTestBase
{
    [Fact]
    public async Task FullBackup_UsesOnlineDatabaseCopiesAndIncludesManagedFilesSettingsAndVerifiedManifest()
    {
        await SetArchiveSettingAsync("phase18", "before");
        var managedPath = Path.Combine(ManagedFiles, "Documents", "2026", "document.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(managedPath)!);
        await File.WriteAllTextAsync(managedPath, "managed-body");
        await File.WriteAllTextAsync(Path.Combine(Settings, "ui.json"), "{\"theme\":\"dark\"}");
        var progress = new List<int>();

        var result = await Service.CreateBackupAsync(
            new BackupRequest(BackupType.Full, Destination),
            (value, _) => { progress.Add(value); return Task.CompletedTask; },
            CancellationToken.None);

        Assert.True(File.Exists(result.PackagePath));
        Assert.True(result.FileSize > 0);
        Assert.Equal(64, result.Sha256.Length);
        Assert.Equal(100, progress[^1]);

        using var archive = ZipFile.OpenRead(result.PackagePath);
        Assert.NotNull(archive.GetEntry("databases/archive.db"));
        Assert.NotNull(archive.GetEntry("databases/audit.db"));
        Assert.NotNull(archive.GetEntry("databases/search.db"));
        Assert.NotNull(archive.GetEntry("files/Documents/2026/document.txt"));
        Assert.NotNull(archive.GetEntry("settings/ui.json"));

        var manifestEntry = archive.GetEntry("manifest.json")!;
        await using var manifestStream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync(
            manifestStream, BackupJsonContext.Default.BackupPackageManifest);
        Assert.Equal(result.BackupUid, manifest!.BackupUid);
        Assert.Equal("full", manifest.BackupType);

        await using var connection = await ConnectionFactory.OpenAsync(DatabaseKind.Archive, CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT status || ':' || file_name || ':' || checksum FROM backup_history WHERE uid = @uid;";
        command.Parameters.AddWithValue("@uid", result.BackupUid.ToString());
        var history = (string)(await command.ExecuteScalarAsync())!;
        Assert.StartsWith("completed:Darhous-full-", history, StringComparison.Ordinal);
        Assert.EndsWith($":{result.Sha256}", history, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(BackupType.Metadata, false, false)]
    [InlineData(BackupType.Configuration, false, true)]
    public async Task BackupTypes_IncludeOnlyTheirDocumentAndSettingsPayloads(
        BackupType type,
        bool expectsManagedFile,
        bool expectsSetting)
    {
        await File.WriteAllTextAsync(Path.Combine(ManagedFiles, "document.bin"), "body");
        await File.WriteAllTextAsync(Path.Combine(Settings, "settings.json"), "settings");

        var result = await Service.CreateBackupAsync(new BackupRequest(type, Destination), null, CancellationToken.None);
        using var archive = ZipFile.OpenRead(result.PackagePath);
        Assert.Equal(expectsManagedFile, archive.GetEntry("files/document.bin") is not null);
        Assert.Equal(expectsSetting, archive.GetEntry("settings/settings.json") is not null);
    }

    [Fact]
    public async Task Backup_DestinationInsideManagedTree_IsRejectedBeforeHistoryOrOutput()
    {
        var nestedDestination = Path.Combine(ManagedFiles, "backup-output");
        Directory.CreateDirectory(nestedDestination);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.CreateBackupAsync(
            new BackupRequest(BackupType.Full, nestedDestination), null, CancellationToken.None));

        Assert.Empty(Directory.EnumerateFiles(nestedDestination));
    }
}
