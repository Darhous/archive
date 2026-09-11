using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Darhous.Archive.Core.Hosting;

/// <summary>
/// Shared Generic Host + Serilog bootstrap used by every executable in the solution
/// (Desktop shell, Workers). Implementation Plan §16 (DI) and §17 (Logging).
/// </summary>
public static class ArchiveHostDefaults
{
    /// <summary>
    /// Root of %ProgramData%\DarhousSmartArchive (Implementation Plan §92).
    /// </summary>
    public static string ProgramDataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "DarhousSmartArchive");

    /// <summary>
    /// Creates a <see cref="HostApplicationBuilder"/> pre-wired with Serilog
    /// (rolling file under Logs\{componentName} + console/debug output) as required
    /// by Implementation Plan §17. Callers add their own services/modules on top.
    /// </summary>
    public static HostApplicationBuilder CreateBuilder(string componentName, string[]? args = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);

        var builder = Host.CreateApplicationBuilder(args ?? []);
        builder.Environment.ApplicationName = componentName;

        var logDirectory = Path.Combine(ProgramDataRoot, "Logs", componentName);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("Component", componentName)
            .WriteTo.Console()
            .WriteTo.Debug()
            .WriteTo.File(
                Path.Combine(logDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                restrictedToMinimumLevel: LogEventLevel.Information)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(dispose: true);

        return builder;
    }
}
