using Darhous.Archive.Modules.Reports.Exporting;
using Darhous.Archive.Modules.Reports.Printing;
using Darhous.Archive.Modules.Reports.SavedViews;
using Microsoft.Extensions.DependencyInjection;

namespace Darhous.Archive.Modules.Reports.Tests;

public sealed class PrintAndRegistrationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "darhous-print-tests", Guid.NewGuid().ToString("N"));

    public PrintAndRegistrationTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task PdfPrintService_ConstructsExpectedWindowsShellPrintRequest()
    {
        var pdfPath = Path.Combine(_directory, "report.pdf");
        await File.WriteAllBytesAsync(pdfPath, "%PDF"u8.ToArray());
        var launcher = new CapturingPrintLauncher();
        var service = new PdfPrintService(launcher);

        await service.PrintAsync(pdfPath, CancellationToken.None);

        Assert.NotNull(launcher.Request);
        Assert.Equal(Path.GetFullPath(pdfPath), launcher.Request.FileName);
        Assert.Equal("print", launcher.Request.Verb);
        Assert.True(launcher.Request.UseShellExecute);
    }

    [Fact]
    public async Task PdfPrintService_RejectsMissingAndNonPdfFiles()
    {
        var service = new PdfPrintService(new CapturingPrintLauncher());

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.PrintAsync(Path.Combine(_directory, "missing.pdf"), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PrintAsync(Path.Combine(_directory, "report.txt"), CancellationToken.None));
    }

    [Fact]
    public void AddReportsModule_RegistersAllExportersAndPublicServices()
    {
        var services = new ServiceCollection();

        services.AddReportsModule();
        using var provider = services.BuildServiceProvider();

        Assert.Equal(3, provider.GetServices<IReportExporter>().Count());
        Assert.IsType<ReportExportService>(provider.GetRequiredService<IReportExportService>());
        Assert.IsType<SavedViewReportService>(provider.GetRequiredService<ISavedViewReportService>());
        Assert.IsType<PdfPrintService>(provider.GetRequiredService<IPdfPrintService>());
    }

    private sealed class CapturingPrintLauncher : IPrintProcessLauncher
    {
        public PrintProcessRequest? Request { get; private set; }

        public void Launch(PrintProcessRequest request) => Request = request;
    }
}
