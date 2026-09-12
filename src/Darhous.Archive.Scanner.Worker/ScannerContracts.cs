using System.Text.Json.Serialization;

namespace Darhous.Archive.Scanner.Worker;

public static class ScannerMessageTypes
{
    public const string Request = "scan.request";
    public const string Result = "scan.result";
    public const string ProtocolError = "protocol.error";
}

[JsonConverter(typeof(JsonStringEnumConverter<ScanColorMode>))]
public enum ScanColorMode { Color, Grayscale, BlackWhite }
[JsonConverter(typeof(JsonStringEnumConverter<ScanSource>))]
public enum ScanSource { Adf, Flatbed }
[JsonConverter(typeof(JsonStringEnumConverter<PageSeparation>))]
public enum PageSeparation { FullBatch, EveryPage, EveryTwoPages, EveryNPages }
[JsonConverter(typeof(JsonStringEnumConverter<ScanPaperSize>))]
public enum ScanPaperSize { A4, Letter, Legal }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScanProfile
{
    public string? DeviceId { get; init; }
    public int Resolution { get; init; } = 300;
    public ScanColorMode ColorMode { get; init; } = ScanColorMode.Color;
    public bool Duplex { get; init; }
    public ScanSource Source { get; init; } = ScanSource.Adf;
    public PageSeparation Separation { get; init; } = PageSeparation.FullBatch;
    public int PagesPerFile { get; init; } = 1;
    public ScanPaperSize PageSize { get; init; } = ScanPaperSize.A4;

    public void Validate()
    {
        if (Resolution is < 75 or > 1200 || !Enum.IsDefined(ColorMode) ||
            !Enum.IsDefined(Source) || !Enum.IsDefined(Separation) || !Enum.IsDefined(PageSize) ||
            (Duplex && Source == ScanSource.Flatbed) ||
            (Separation == PageSeparation.EveryNPages && PagesPerFile is < 1 or > 1000))
        {
            throw new ArgumentException("Invalid scan profile: DPI 75–1200; duplex requires ADF; N must be 1–1000; enum values must be defined.");
        }
    }

    public int GroupSize => Separation switch
    {
        PageSeparation.FullBatch => int.MaxValue,
        PageSeparation.EveryPage => 1,
        PageSeparation.EveryTwoPages => 2,
        PageSeparation.EveryNPages => PagesPerFile,
        _ => throw new ArgumentException("Unknown page separation mode."),
    };
}

public sealed record ScannerDevice(string Id, string Name);
public sealed record ScanFailure(string Code, string Message);
public sealed record ScanBatch(IReadOnlyList<string> Files, ScanFailure? Failure = null)
{
    public static ScanBatch Failed(string code, string message) => new([], new(code, message));
}

/// <summary>Sequential use only. Writes complete PDFs exclusively inside outputDirectory.
/// The caller owns that directory and cleans failed batches; successful files belong to the host.</summary>
public interface IScannerEngine : IDisposable
{
    Task<IReadOnlyList<ScannerDevice>> GetDevicesAsync(CancellationToken cancellationToken);
    Task<ScanBatch> ScanAsync(ScanProfile profile, string outputDirectory, CancellationToken cancellationToken);
}
