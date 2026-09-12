using System.Drawing.Imaging;
using Darhous.Archive.Scanner.Worker;
using NAPS2.Images;
using NAPS2.ImportExport;
using NAPS2.Pdf;
using NAPS2.Scan;
using PdfSharpCore.Pdf.IO;

namespace Darhous.Archive.Scanner.Worker.Tests;

public sealed class Naps2AdapterTests
{
    [Theory]
    [InlineData(PageSeparation.FullBatch, 1, 1)]
    [InlineData(PageSeparation.EveryPage, 1, 5)]
    [InlineData(PageSeparation.EveryTwoPages, 2, 3)]
    [InlineData(PageSeparation.EveryNPages, 3, 2)]
    public async Task RealExporter_GroupsFiveSyntheticPages(PageSeparation separation, int n, int expectedFiles)
    {
        var root = Path.Combine(Path.GetTempPath(), "Darhous.Scanner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var context = new ScanningContext(new WindowsImageContext());
            using var engine = new Naps2ScannerEngine(_ => Task.FromResult(new List<ScanDevice>
                { new(Driver.Wia, "fake", "Fake") }), (_, ct) => Pages(ct));
            var result = await engine.ScanAsync(new ScanProfile { Separation = separation, PagesPerFile = n }, root, CancellationToken.None);
            Assert.Null(result.Failure);
            Assert.Equal(expectedFiles, result.Files.Count);
            var totalPages = 0;
            foreach (var file in result.Files)
            {
                Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString((await File.ReadAllBytesAsync(file))[..5]));
                using var document = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                totalPages += document.PageCount;
                Assert.InRange(document.PageCount, 1, new ScanProfile { Separation = separation, PagesPerFile = n }.GroupSize);
            }
            Assert.Equal(5, totalPages);

            async IAsyncEnumerable<ProcessedImage> Pages([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            {
                for (var i = 0; i < 5; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    using var bitmap = new Bitmap(20, 30);
                    using var stream = new MemoryStream();
                    bitmap.Save(stream, ImageFormat.Png);
                    stream.Position = 0;
                    await foreach (var image in new ImageImporter(context).Import(stream)) yield return image;
                }
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task NoDevices_ReturnsClearFailure()
    {
        using var engine = new Naps2ScannerEngine(_ => Task.FromResult(new List<ScanDevice>()),
            (_, _) => throw new InvalidOperationException("Must not acquire without a device"));
        Assert.Empty(await engine.GetDevicesAsync(CancellationToken.None));
        var result = await engine.ScanAsync(new ScanProfile(), Path.GetTempPath(), CancellationToken.None);
        Assert.Equal("no_scanner", result.Failure!.Code);
        Assert.Empty(result.Files);
    }

    [Theory]
    [InlineData(ScanColorMode.Color, ScanSource.Adf, false, BitDepth.Color, PaperSource.Feeder)]
    [InlineData(ScanColorMode.Grayscale, ScanSource.Adf, true, BitDepth.Grayscale, PaperSource.Duplex)]
    [InlineData(ScanColorMode.BlackWhite, ScanSource.Flatbed, false, BitDepth.BlackAndWhite, PaperSource.Flatbed)]
    public void Profile_MapsToActualNaps2Options(ScanColorMode color, ScanSource source, bool duplex, BitDepth depth, PaperSource paperSource)
    {
        var device = new ScanDevice(Driver.Wia, "fake", "Fake device");
        var options = Naps2ScannerEngine.MapOptions(new ScanProfile
        {
            ColorMode = color, Source = source, Duplex = duplex, Resolution = 600, PageSize = ScanPaperSize.Letter,
        }, device);
        Assert.Same(device, options.Device);
        Assert.Equal(depth, options.BitDepth);
        Assert.Equal(paperSource, options.PaperSource);
        Assert.Equal(600, options.Dpi);
        Assert.Equal(PageSize.Letter, options.PageSize);
        Assert.False(options.UseNativeUI);
    }

    [Fact]
    public async Task WindowsBackend_ImportsSyntheticImageAndExportsRealPdf()
    {
        var root = Path.Combine(Path.GetTempPath(), "Darhous.Scanner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var png = Path.Combine(root, "source.png");
            using (var bitmap = new Bitmap(120, 180))
            {
                bitmap.SetResolution(300, 300);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.White);
                graphics.FillRectangle(Brushes.Black, 10, 10, 50, 50);
                bitmap.Save(png, ImageFormat.Png);
            }
            using var context = new ScanningContext(new WindowsImageContext());
            var pages = new List<ProcessedImage>();
            await foreach (var page in new ImageImporter(context).Import(png)) pages.Add(page);
            Assert.Single(pages);
            var pdf = Path.Combine(root, "output.pdf");
            Assert.True(await new PdfExporter(context).Export(pdf, pages));
            Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString((await File.ReadAllBytesAsync(pdf))[..5]));
            Assert.True(new FileInfo(pdf).Length > 100);
        }
        finally { Directory.Delete(root, true); }
    }
}
