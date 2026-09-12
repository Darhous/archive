namespace Darhous.Archive.Modules.Reports.Printing;

public interface IPdfPrintService
{
    Task PrintAsync(string pdfPath, CancellationToken cancellationToken);
}
