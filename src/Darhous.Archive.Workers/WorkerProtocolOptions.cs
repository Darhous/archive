using Darhous.Archive.Configuration;

namespace Darhous.Archive.Workers;

/// <summary>
/// Configures the worker wire protocol. Tests can override every environment-dependent
/// value so they never use the application's real pipe namespace or managed storage.
/// </summary>
public sealed class WorkerProtocolOptions
{
    public string PipeNamePrefix { get; init; } = "Darhous.Archive.Worker";

    public int ProtocolVersion { get; init; } = 1;

    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public string ManagedTempStorageRoot { get; init; } = Path.Combine(AppPaths.Temp, "Workers");

    public int MaximumFrameLengthBytes { get; init; } = 16 * 1024 * 1024;
}
