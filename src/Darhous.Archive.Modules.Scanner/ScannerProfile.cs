namespace Darhous.Archive.Modules.Scanner;

public sealed record ScannerProfile(
    int Id,
    string Name,
    int Resolution,
    string ColorMode,
    bool Duplex,
    string Source,
    string PageSeparationMode,
    bool IsSeed
);
