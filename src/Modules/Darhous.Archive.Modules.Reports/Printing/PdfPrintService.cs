namespace Darhous.Archive.Modules.Reports.Printing;

public sealed class PdfPrintService(IPrintProcessLauncher processLauncher) : IPdfPrintService
{
    public Task PrintAsync(string pdfPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(pdfPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only generated PDF documents can be printed.", nameof(pdfPath));
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The generated PDF document was not found.", fullPath);
        }

        processLauncher.Launch(new PrintProcessRequest(fullPath, "print", UseShellExecute: true));
        return Task.CompletedTask;
    }
}
