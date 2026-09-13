using System.IO.Compression;
using System.Text.Json;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Jobs;
using Darhous.Archive.Modules.Updates.Sources;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Darhous.Archive.Modules.Updates.Tests;

public sealed class UpdateFlowTests : UpdateTestBase
{
    [Fact]
    public async Task CheckAsync_ReturnsNewestMatchingApplicationUpdate()
    {
        var feed = new UpdateFeedManifest(1,
        [
            new UpdateFeedItem("application", "other", "9.0.0", "other.darhousupdate", false),
            new UpdateFeedItem(UpdateModuleOptions.ApplicationComponentType, UpdateModuleOptions.ApplicationComponentId, "1.1.0", "old.darhousupdate", false),
            new UpdateFeedItem(UpdateModuleOptions.ApplicationComponentType, UpdateModuleOptions.ApplicationComponentId, "2.0.0", "latest.darhousupdate", true),
        ]);
        await File.WriteAllTextAsync(FeedPath, JsonSerializer.Serialize(feed));

        var result = await Provider.GetRequiredService<IUpdateService>().CheckAsync(CancellationToken.None);

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(new Version(2, 0, 0), result.Latest!.Version);
        Assert.True(result.Latest.IncludesMigration);
        Assert.Equal(Path.Combine(Root, "latest.darhousupdate"), result.Latest.PackagePath);
    }

    [Fact]
    public async Task ManualInstall_StagesAppliesAndRollbackRestoresTouchedFiles()
    {
        var certificate = UpdatePackageBuilder.CreateCertificate();
        TrustStore.Trust(certificate.Thumbprint);
        var target = Path.Combine(InstallDirectory, "app.txt");
        await File.WriteAllTextAsync(target, "old-version");
        var package = UpdatePackageBuilder.Build(PackageDirectory, certificate);

        var service = Provider.GetRequiredService<IUpdateService>();
        var result = await service.InstallAsync(package, null, null, CancellationToken.None);

        Assert.Equal("new-version", await File.ReadAllTextAsync(target));
        Assert.True(File.Exists(Path.Combine(result.RollbackPointPath, "rollback.json")));
        Assert.Null(result.SafetyBackupPath);

        await service.RollbackAsync(result.HistoryId, null, null, CancellationToken.None);

        Assert.Equal("old-version", await File.ReadAllTextAsync(target));
        Assert.Equal("1.0.0", await File.ReadAllTextAsync(Path.Combine(Root, "active-version.txt")));
        Assert.Equal("rolled_back", await ReadScalarAsync<string>(
            "SELECT status FROM update_history WHERE id = @id;", result.HistoryId));
    }

    [Fact]
    public async Task MigratingInstall_CreatesAndVerifiesRealPhase18MetadataBackupBeforeApply()
    {
        var certificate = UpdatePackageBuilder.CreateCertificate();
        TrustStore.Trust(certificate.Thumbprint);
        var package = UpdatePackageBuilder.Build(
            PackageDirectory, certificate, includesMigration: true);

        var result = await Provider.GetRequiredService<IUpdateService>()
            .InstallAsync(package, null, null, CancellationToken.None);

        Assert.NotNull(result.SafetyBackupPath);
        Assert.True(File.Exists(result.SafetyBackupPath));
        using (var archive = ZipFile.OpenRead(result.SafetyBackupPath))
        {
            Assert.NotNull(archive.GetEntry("manifest.json"));
            Assert.NotNull(archive.GetEntry("databases/archive.db"));
            Assert.NotNull(archive.GetEntry("databases/audit.db"));
            Assert.NotNull(archive.GetEntry("databases/search.db"));
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("files/", StringComparison.Ordinal));
        }

        Assert.Equal("completed", await ReadScalarAsync<string>(
            "SELECT status FROM backup_history ORDER BY id DESC LIMIT 1;"));
        Assert.Equal("metadata", await ReadScalarAsync<string>(
            "SELECT backup_type FROM backup_history ORDER BY id DESC LIMIT 1;"));
        Assert.Equal("new-version", await File.ReadAllTextAsync(Path.Combine(InstallDirectory, "app.txt")));
    }

    [Fact]
    public async Task BackupFailure_BlocksMigratingUpdateBeforeStageOrApply()
    {
        var certificate = UpdatePackageBuilder.CreateCertificate();
        TrustStore.Trust(certificate.Thumbprint);
        BackupGate.FailCreate = true;
        var target = Path.Combine(InstallDirectory, "app.txt");
        await File.WriteAllTextAsync(target, "old-version");
        var package = UpdatePackageBuilder.Build(
            PackageDirectory, certificate, includesMigration: true);

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            Provider.GetRequiredService<IUpdateService>()
                .InstallAsync(package, null, null, CancellationToken.None));

        Assert.Equal("Injected backup failure.", exception.Message);
        Assert.Equal("old-version", await File.ReadAllTextAsync(target));
        Assert.False(Directory.Exists(Path.Combine(Root, "staging")));
        Assert.False(Directory.Exists(Path.Combine(Root, "rollback")));
        Assert.Equal("failed", await ReadScalarAsync<string>(
            "SELECT status FROM update_history ORDER BY id DESC LIMIT 1;"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task InvalidSignatureOrChecksum_IsRejectedBeforeHistoryAndMutation(
        bool omitSignature,
        bool corruptPayload)
    {
        var certificate = UpdatePackageBuilder.CreateCertificate();
        TrustStore.Trust(certificate.Thumbprint);
        var package = UpdatePackageBuilder.Build(
            PackageDirectory, certificate,
            corruptPayload: corruptPayload,
            omitSignature: omitSignature);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Provider.GetRequiredService<IUpdateService>()
                .InstallAsync(package, null, null, CancellationToken.None));

        Assert.False(File.Exists(Path.Combine(InstallDirectory, "app.txt")));
        Assert.Equal(0L, await ReadScalarAsync<long>("SELECT COUNT(*) FROM update_history;"));
    }

    [Fact]
    public async Task ManualRequest_QueuedJobPerformsActualInstallThroughJobRunner()
    {
        var certificate = UpdatePackageBuilder.CreateCertificate();
        TrustStore.Trust(certificate.Thumbprint);
        var packagePath = UpdatePackageBuilder.Build(PackageDirectory, certificate);

        var jobUid = await Provider.GetRequiredService<IUpdateRequestService>()
            .QueueInstallAsync(packagePath, null, CancellationToken.None);
        await Provider.GetRequiredService<JobRunner>().RunOnceAsync(CancellationToken.None);
        var job = await Provider.GetRequiredService<IUnitOfWork>().ExecuteAsync(
            (context, ct) => context.Jobs.GetByUidAsync(jobUid, ct),
            CancellationToken.None);

        Assert.NotNull(job);
        Assert.Equal("UpdateJob", job.JobType);
        Assert.Equal(JobStatus.Succeeded, job.Status);
        using var payload = JsonDocument.Parse(job.PayloadJson!);
        Assert.Equal(
            Path.GetFullPath(packagePath),
            payload.RootElement.GetProperty("packagePath").GetString());
        Assert.Equal("new-version", await File.ReadAllTextAsync(Path.Combine(InstallDirectory, "app.txt")));
    }

    [Fact]
    public async Task StartupAutoCheck_WithAutoInstall_QueuesAvailablePackage()
    {
        var packagePath = Path.Combine(PackageDirectory, "auto.darhousupdate");
        var feed = new UpdateFeedManifest(1,
        [
            new UpdateFeedItem(
                UpdateModuleOptions.ApplicationComponentType,
                UpdateModuleOptions.ApplicationComponentId,
                "2.0.0",
                packagePath,
                false),
        ]);
        await File.WriteAllTextAsync(FeedPath, JsonSerializer.Serialize(feed));
        await Provider.GetRequiredService<IUpdateSettingsService>().SaveAsync(
            new UpdateUserSettings(AutoCheck: true, AutoInstall: true),
            CancellationToken.None);

        await Provider.GetRequiredService<UpdateStartupService>().StartAsync(CancellationToken.None);

        Assert.Equal(1L, await ReadScalarAsync<long>(
            "SELECT COUNT(*) FROM jobs WHERE job_type = 'UpdateJob' AND status = 'pending';"));
    }

    [Fact]
    public async Task Settings_DefaultThenPersistedValues_AreBackedByAppSettingsTable()
    {
        var service = Provider.GetRequiredService<IUpdateSettingsService>();
        Assert.Equal(new UpdateUserSettings(AutoCheck: true, AutoInstall: false),
            await service.GetAsync(CancellationToken.None));

        await service.SaveAsync(new UpdateUserSettings(AutoCheck: false, AutoInstall: true), CancellationToken.None);

        Assert.Equal(new UpdateUserSettings(AutoCheck: false, AutoInstall: true),
            await service.GetAsync(CancellationToken.None));
        Assert.Equal("false", await ReadScalarAsync<string>(
            "SELECT value_json FROM app_settings WHERE setting_key = 'updates.auto_check';"));
    }

    private async Task<T> ReadScalarAsync<T>(string sql, long? id = null)
    {
        var factory = Provider.GetRequiredService<ISqliteConnectionFactory>();
        await using var connection = await factory.OpenAsync(DatabaseKind.Archive, CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (id.HasValue) command.Parameters.AddWithValue("@id", id.Value);
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
