namespace Darhous.Archive.Modules.Scanner;

public enum ScanColorMode { Color, Grayscale, BlackWhite }
public enum ScanSource { Adf, Flatbed }
public enum PageSeparation { FullBatch, EveryPage, EveryTwoPages, EveryNPages }
public enum ScanPaperSize { A4, Letter, Legal }

public sealed record ScanProfileRequest
{
    public string? DeviceId { get; init; }
    public int Resolution { get; init; } = 300;
    public ScanColorMode ColorMode { get; init; } = ScanColorMode.Color;
    public bool Duplex { get; init; }
    public ScanSource Source { get; init; } = ScanSource.Adf;
    public PageSeparation Separation { get; init; } = PageSeparation.FullBatch;
    public int PagesPerFile { get; init; } = 1;
    public ScanPaperSize PageSize { get; init; } = ScanPaperSize.A4;
}

public sealed record ScanFailure(string Code, string Message);
