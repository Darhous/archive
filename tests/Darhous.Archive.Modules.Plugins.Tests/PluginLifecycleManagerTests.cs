namespace Darhous.Archive.Modules.Plugins.Tests;

public class PluginLifecycleManagerTests : PluginsTestBase
{
    private const string PluginId = "Darhous.Test.SamplePlugin";

    private Task<string> BuildAndTrustPackageAsync(TestPackageBuilder.BuildOptions? options = null)
    {
        var certificate = TestPackageBuilder.CreateSigningCertificate();
        var effectiveOptions = options is null ? new TestPackageBuilder.BuildOptions() : options with { };
        effectiveOptions = effectiveOptions with { SigningCertificate = certificate };

        var packagePath = TestPackageBuilder.Build(PackageDirectory, effectiveOptions);
        TrustStore.Trust(certificate.Thumbprint);
        return Task.FromResult(packagePath);
    }

    [Fact]
    public async Task InstallAsync_ValidSignedPackage_Succeeds()
    {
        var packagePath = await BuildAndTrustPackageAsync();

        var result = await LifecycleManager.InstallAsync(packagePath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PluginId, result.Value.Id);
    }

    [Fact]
    public async Task InstallAsync_UntrustedSigner_Fails()
    {
        // Signed, but its certificate is never added to the trust store.
        var certificate = TestPackageBuilder.CreateSigningCertificate();
        var packagePath = TestPackageBuilder.Build(PackageDirectory, new TestPackageBuilder.BuildOptions { SigningCertificate = certificate });

        var result = await LifecycleManager.InstallAsync(packagePath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("PLUGIN_VALIDATION_FAILED", result.Error!.Code);
        Assert.Contains("not in the trusted publisher list", result.Error.Message);
    }

    [Fact]
    public async Task InstallAsync_MissingSignature_Fails()
    {
        var packagePath = TestPackageBuilder.Build(PackageDirectory, new TestPackageBuilder.BuildOptions { OmitSignature = true });

        var result = await LifecycleManager.InstallAsync(packagePath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("signature.p7s", result.Error!.Message);
    }

    [Fact]
    public async Task InstallAsync_CorruptChecksum_Fails()
    {
        var certificate = TestPackageBuilder.CreateSigningCertificate();
        var packagePath = TestPackageBuilder.Build(PackageDirectory, new TestPackageBuilder.BuildOptions { CorruptChecksum = true, SigningCertificate = certificate });
        TrustStore.Trust(certificate.Thumbprint);

        var result = await LifecycleManager.InstallAsync(packagePath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Checksum mismatch", result.Error!.Message);
    }

    [Fact]
    public async Task InstallAsync_IncompatibleCoreVersion_Fails()
    {
        var packagePath = await BuildAndTrustPackageAsync(new TestPackageBuilder.BuildOptions { MinCoreVersion = "99.0.0" });

        var result = await LifecycleManager.InstallAsync(packagePath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Incompatible", result.Error!.Message);
    }

    [Fact]
    public async Task EnableAsync_WithoutGrantedPermissions_Fails()
    {
        var packagePath = await BuildAndTrustPackageAsync(new TestPackageBuilder.BuildOptions
        {
            Permissions = ["documents.read"],
            SigningCertificate = TestPackageBuilder.CreateSigningCertificate(),
        });
        await LifecycleManager.InstallAsync(packagePath, CancellationToken.None);

        var result = await LifecycleManager.EnableAsync(PluginId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("PLUGIN_PERMISSIONS_NOT_GRANTED", result.Error!.Code);
    }

    [Fact]
    public async Task EnableAsync_AfterInstallAndGrant_StartsThePluginAndRegistersHealth()
    {
        var packagePath = await BuildAndTrustPackageAsync(new TestPackageBuilder.BuildOptions { Permissions = ["documents.read"] });
        await LifecycleManager.InstallAsync(packagePath, CancellationToken.None);
        await LifecycleManager.GrantPermissionAsync(PluginId, "documents.read", Guid.NewGuid(), CancellationToken.None);

        var result = await LifecycleManager.EnableAsync(PluginId, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var record = await UnitOfWork.ExecuteAsync((context, ct) => context.Plugins.GetByPluginIdAsync(PluginId, ct), CancellationToken.None);
        Assert.Equal("healthy", record!.Status);

        var healthReports = await HealthRegistry.CheckAllAsync(CancellationToken.None);
        Assert.Contains(healthReports, r => r.Component == PluginId);
    }

    [Fact]
    public async Task DisableAsync_StopsThePluginAndUnregistersHealth()
    {
        var packagePath = await BuildAndTrustPackageAsync();
        await LifecycleManager.InstallAsync(packagePath, CancellationToken.None);
        await LifecycleManager.EnableAsync(PluginId, CancellationToken.None);

        var result = await LifecycleManager.DisableAsync(PluginId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var record = await UnitOfWork.ExecuteAsync((context, ct) => context.Plugins.GetByPluginIdAsync(PluginId, ct), CancellationToken.None);
        Assert.Equal("disabled", record!.Status);

        var healthReports = await HealthRegistry.CheckAllAsync(CancellationToken.None);
        Assert.DoesNotContain(healthReports, r => r.Component == PluginId);
    }

    [Fact]
    public async Task RemoveAsync_DeletesRegistryRowAndInstallDirectory()
    {
        var packagePath = await BuildAndTrustPackageAsync();
        await LifecycleManager.InstallAsync(packagePath, CancellationToken.None);
        var installedDirectory = Path.Combine(PluginsRoot, PluginId);
        Assert.True(Directory.Exists(installedDirectory));

        await LifecycleManager.RemoveAsync(PluginId, CancellationToken.None);

        var record = await UnitOfWork.ExecuteAsync((context, ct) => context.Plugins.GetByPluginIdAsync(PluginId, ct), CancellationToken.None);
        Assert.Null(record);
        Assert.False(Directory.Exists(installedDirectory));
    }

    [Fact]
    public async Task EnableAsync_ThreeCrashesWithinWindow_Quarantines()
    {
        var packagePath = await BuildAndTrustPackageAsync();
        await LifecycleManager.InstallAsync(packagePath, CancellationToken.None);

        // Drop the flag TestPlugin checks before throwing, in its installed bin directory.
        var installedBinDirectory = Path.Combine(PluginsRoot, PluginId, "1.0.0", "bin");
        File.WriteAllText(Path.Combine(installedBinDirectory, "THROW_ON_START"), "");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = await LifecycleManager.EnableAsync(PluginId, CancellationToken.None);
            Assert.False(result.IsSuccess);
        }

        var record = await UnitOfWork.ExecuteAsync((context, ct) => context.Plugins.GetByPluginIdAsync(PluginId, ct), CancellationToken.None);
        Assert.Equal("quarantined", record!.Status);

        var enableAfterQuarantine = await LifecycleManager.EnableAsync(PluginId, CancellationToken.None);
        Assert.Equal("PLUGIN_QUARANTINED", enableAfterQuarantine.Error!.Code);
    }

    [Fact]
    public async Task EnableAsync_UnmetDependency_Fails()
    {
        var packagePath = await BuildAndTrustPackageAsync(new TestPackageBuilder.BuildOptions
        {
            Dependencies = [new { id = "Darhous.NotInstalled", version = ">=1.0.0" }],
        });
        await LifecycleManager.InstallAsync(packagePath, CancellationToken.None);

        var result = await LifecycleManager.EnableAsync(PluginId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("PLUGIN_DEPENDENCY_UNMET", result.Error!.Code);
    }
}
