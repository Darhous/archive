using Darhous.Archive.Contracts.Health;
using Darhous.Archive.Core.Health;
using Darhous.Archive.PluginSdk;
using Darhous.Archive.PluginSdk.Configuration;
using Darhous.Archive.PluginSdk.Runtime;

namespace Darhous.TestPlugin;

/// <summary>
/// A real, minimal, compiled IArchivePlugin used ONLY to prove the Plugin Platform pipeline
/// end-to-end — Darhous.Archive.Modules.Plugins.Tests packages this project's own build
/// output into a real .archiveplugin ZIP. Not shipped with the app.
///
/// Signals lifecycle calls via marker files next to its own assembly (not static fields —
/// this type gets loaded into a separate collectible <c>AssemblyLoadContext</c> at test time,
/// which has its own independent copy of any static state; only the filesystem is a reliable
/// way for a test to observe what a dynamically-loaded plugin actually did).
/// </summary>
public sealed class TestPlugin : IArchivePlugin
{
    public PluginIdentity Identity { get; } = new("Darhous.Test.SamplePlugin", new Version(1, 0, 0), "Darhous", "Sample Test Plugin");

    /// <summary>
    /// Historically this pointed at <c>Path.GetDirectoryName(typeof(TestPlugin).Assembly.Location)</c>,
    /// but the host loads plugin assemblies from a byte stream (so it never keeps the DLL file
    /// memory-mapped/locked on Windows), and .NET reports an empty <see cref="System.Reflection.Assembly.Location"/>
    /// for stream-loaded assemblies — so this must come from <see cref="IPluginEnvironment.InstallDirectory"/> instead.
    /// </summary>
    private static string ThrowOnStartFlagPath(IPluginRuntimeContext context) => Path.Combine(context.Environment.InstallDirectory, "bin", "THROW_ON_START");

    public ValueTask ConfigureAsync(IPluginConfigurationContext context, CancellationToken cancellationToken)
    {
        context.Services.AddSingleton<IHealthContributor>(new TestPluginHealthContributor());
        return ValueTask.CompletedTask;
    }

    public ValueTask StartAsync(IPluginRuntimeContext context, CancellationToken cancellationToken)
    {
        BumpMarker(context, "started.marker");

        if (File.Exists(ThrowOnStartFlagPath(context)))
        {
            throw new InvalidOperationException("Simulated startup failure for crash-loop testing.");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    private static void BumpMarker(IPluginRuntimeContext context, string fileName)
    {
        var path = Path.Combine(context.Environment.InstallDirectory, "bin", fileName);
        var count = File.Exists(path) && int.TryParse(File.ReadAllText(path), out var existing) ? existing : 0;
        File.WriteAllText(path, (count + 1).ToString());
    }

    private sealed class TestPluginHealthContributor : IHealthContributor
    {
        public string Component => "Darhous.Test.SamplePlugin";

        public Task<HealthReport> CheckAsync(CancellationToken cancellationToken) => Task.FromResult(HealthReport.Healthy(Component));
    }
}
