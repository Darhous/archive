using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Results;
using Darhous.Archive.Modules.Plugins.Hosting;
using Darhous.Archive.Modules.Plugins.Packaging;
using Darhous.Archive.PluginSdk;

namespace Darhous.Archive.Modules.Plugins.Lifecycle;

/// <summary>
/// Plugin SDK §39 (Atomic Installation): Temp staging → Validation → Install to versioned
/// directory → Register → Health check → Commit. "Health check" here means a structural
/// dry-load (the assembly loads, the entry type exists and implements IArchivePlugin) — NOT
/// actually calling ConfigureAsync/StartAsync, which only happens later via Enable. Layout
/// matches Plugin SDK §40: <c>Plugins\&lt;PluginId&gt;\&lt;Version&gt;\</c> + a <c>current.json</c>
/// pointing at the active version.
/// </summary>
public sealed class PluginInstaller(IUnitOfWork unitOfWork, PluginPackageValidator validator, PluginPlatformOptions options)
{
    public async Task<Result<PluginManifest>> InstallAsync(string packagePath, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(packagePath);
        if (!validation.IsValid)
        {
            return Result<PluginManifest>.Failure(Error.Of("PLUGIN_VALIDATION_FAILED", string.Join(" | ", validation.Errors)));
        }

        var manifest = validation.Manifest!;
        var stagingDirectory = Path.Combine(Path.GetTempPath(), "darhous-plugin-staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            using (var package = PluginPackage.Open(packagePath))
            {
                package.ExtractTo(stagingDirectory);

                // manifest.json is package metadata, not payload (ExtractTo skips it) — but
                // Enable/Update/Rollback need to re-read it later without the original
                // .archiveplugin file around, so it's written into the install directory too.
                File.WriteAllText(Path.Combine(stagingDirectory, "manifest.json"), package.RawManifestJson);
            }

            var dryLoadError = InProcessPluginHost.DryLoad(stagingDirectory, manifest.EntryPoint, manifest.EntryType);
            if (dryLoadError is not null)
            {
                return Result<PluginManifest>.Failure(Error.Of("PLUGIN_HEALTH_CHECK_FAILED", dryLoadError));
            }

            var versionedDirectory = GetVersionedDirectory(manifest.Id, manifest.Version);
            if (Directory.Exists(versionedDirectory))
            {
                return Result<PluginManifest>.Failure(Error.Of("PLUGIN_VERSION_ALREADY_INSTALLED", $"{manifest.Id} {manifest.Version} is already installed."));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(versionedDirectory)!);
            Directory.Move(stagingDirectory, versionedDirectory);

            WriteCurrentVersionMarker(manifest.Id, manifest.Version);

            await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
            {
                await context.Plugins.CreateAsync(
                    new NewPluginRecord(manifest.Id, manifest.Version, manifest.Version, manifest.Publisher,
                        manifest.TrustLevel.ToLowerInvariant(), "disabled", manifest.UpdateChannel ?? "stable", validation.PackageHash!),
                    ct);

                // Every requested permission starts un-granted — Plugin SDK §76: shown to the
                // user before install; an Admin must explicitly approve each one before Enable.
                foreach (var permission in manifest.Permissions)
                {
                    await context.Plugins.SetPermissionAsync(manifest.Id, permission, granted: false, grantedBy: null, grantedAt: null, ct);
                }

                return null;
            }, cancellationToken);

            return Result<PluginManifest>.Success(manifest);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    public string GetVersionedDirectory(string pluginId, string version) =>
        Path.Combine(options.PluginsRoot, pluginId, version);

    public string GetCurrentVersionMarkerPath(string pluginId) =>
        Path.Combine(options.PluginsRoot, pluginId, "current.json");

    public void WriteCurrentVersionMarker(string pluginId, string version)
    {
        var markerPath = GetCurrentVersionMarkerPath(pluginId);
        Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
        File.WriteAllText(markerPath, System.Text.Json.JsonSerializer.Serialize(new { version }));
    }
}
