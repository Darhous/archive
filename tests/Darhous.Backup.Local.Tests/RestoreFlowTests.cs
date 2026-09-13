using System.IO.Compression;
using Darhous.Backup.Local.Packaging;
using Microsoft.Data.Sqlite;

namespace Darhous.Backup.Local.Tests;

public sealed class RestoreFlowTests : LocalBackupTestBase
{
    [Fact]
    public async Task Restore_ValidatesSafetyBacksUpPausesRestoresMigratesRebuildsAndVerifies()
    {
        await SetArchiveSettingAsync("restore-probe", "backed-up-value");
        var managedPath = Path.Combine(ManagedFiles, "document.txt");
        var settingsPath = Path.Combine(Settings, "ui.json");
        await File.WriteAllTextAsync(managedPath, "backed-up-document");
        await File.WriteAllTextAsync(settingsPath, "backed-up-settings");
        var backup = await Service.CreateBackupAsync(
            new BackupRequest(BackupType.Full, Destination), null, CancellationToken.None);

        await SetArchiveSettingAsync("restore-probe", "live-value-after-backup");
        await File.WriteAllTextAsync(managedPath, "live-document-after-backup");
        await File.WriteAllTextAsync(settingsPath, "live-settings-after-backup");

        var result = await Service.RestoreAsync(backup.PackagePath, requestedBy: null, CancellationToken.None);

        Assert.Equal("backed-up-value", await ReadArchiveSettingAsync("restore-probe"));
        Assert.Equal("backed-up-document", await File.ReadAllTextAsync(managedPath));
        Assert.Equal("backed-up-settings", await File.ReadAllTextAsync(settingsPath));
        Assert.Equal(1, Rebuilder.Calls);
        Assert.True(result.RequiresRestart);
        Assert.True(File.Exists(result.SafetyBackupPath));
        Assert.Contains("Darhous-full-", Path.GetFileName(result.SafetyBackupPath), StringComparison.Ordinal);

        var safetyArchivePath = Path.Combine(Root, "safety-archive.db");
        using (var safetyArchive = ZipFile.OpenRead(result.SafetyBackupPath))
        await using (var source = safetyArchive.GetEntry("databases/archive.db")!.Open())
        await using (var destination = File.Create(safetyArchivePath))
        {
            await source.CopyToAsync(destination);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = safetyArchivePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        };
        await using (var safetyConnection = new SqliteConnection(builder.ToString()))
        {
            await safetyConnection.OpenAsync();
            await using var command = safetyConnection.CreateCommand();
            command.CommandText = "SELECT value_json FROM app_settings WHERE setting_key = 'restore-probe';";
            Assert.Equal("live-value-after-backup", (string?)await command.ExecuteScalarAsync());
        }

        await SetArchiveSettingAsync("writers-resumed", "yes");
        Assert.Equal("yes", await ReadArchiveSettingAsync("writers-resumed"));
    }

    [Fact]
    public async Task Restore_ChecksumMismatch_IsRejectedBeforeCurrentStateChangesOrSafetyBackup()
    {
        await SetArchiveSettingAsync("restore-probe", "must-stay");
        var backup = await Service.CreateBackupAsync(
            new BackupRequest(BackupType.Metadata, Destination), null, CancellationToken.None);
        var corruptPath = Path.Combine(Destination, "corrupt.darhousbackup");
        File.Copy(backup.PackagePath, corruptPath);

        using (var archive = ZipFile.Open(corruptPath, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("databases/archive.db")!;
            entry.Delete();
            var replacement = archive.CreateEntry("databases/archive.db");
            await using var stream = replacement.Open();
            await stream.WriteAsync("not-a-database"u8.ToArray());
        }

        var safetyCountBefore = Directory.EnumerateFiles(Safety).Count();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Service.RestoreAsync(corruptPath, requestedBy: null, CancellationToken.None));

        Assert.Equal("must-stay", await ReadArchiveSettingAsync("restore-probe"));
        Assert.Equal(safetyCountBefore, Directory.EnumerateFiles(Safety).Count());
        Assert.Equal(0, Rebuilder.Calls);
    }
}
