namespace Darhous.Archive.Modules.Reports.Printing;

public sealed record PrintProcessRequest(
    string FileName,
    string Verb,
    bool UseShellExecute);
