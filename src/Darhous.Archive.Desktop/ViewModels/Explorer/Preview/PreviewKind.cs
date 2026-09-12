namespace Darhous.Archive.Desktop.ViewModels.Explorer.Preview;

/// <summary>Implementation Plan §59: "PDF Preview, Office preview fallback, Metadata preview" — three distinct preview surfaces, not one universal renderer.</summary>
public enum PreviewKind
{
    None,
    Pdf,
    OfficeFallback,
    Unsupported,
}
