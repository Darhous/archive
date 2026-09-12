using Darhous.Archive.Configuration;
using Darhous.Archive.Workers;
using Darhous.Archive.Workers.Host;

namespace Darhous.Archive.Modules.Ocr;

/// <summary>Environment-dependent OCR host settings; tests replace every filesystem value.</summary>
public sealed class OcrSupervisionOptions
{
    public string ExecutablePath { get; init; } = Path.Combine(AppContext.BaseDirectory, "Darhous.Archive.Ocr.Worker.exe");

    public string ArchiveStorageRoot { get; init; } = AppPaths.ArchiveStorage;

    public WorkerProtocolOptions Protocol { get; init; } = new();

    /// <summary>Uses Phase 13's restart/crash-loop defaults directly.</summary>
    public WorkerSupervisionOptions WorkerSupervision { get; init; } = new();

    public IReadOnlyList<string> DefaultLanguages { get; init; } = Array.AsReadOnly(["ara"]);
}
