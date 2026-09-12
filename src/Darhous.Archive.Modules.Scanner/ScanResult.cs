namespace Darhous.Archive.Modules.Scanner;

public sealed record ScanResult(
    string FilePath,
    bool Success,
    string? ErrorMessage
);
