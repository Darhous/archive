using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Darhous.Archive.Ocr.Worker;

/// <summary>
/// Default §41.3 provider. PdfPig supplies each scanned page's embedded raster to Tesseract;
/// PDFsharp imports the original PDF pages unchanged and appends only transparent text.
/// </summary>
public sealed class TesseractOcrEngine : IOcrEngine
{
    private readonly string _trainedDataDirectory;

    public TesseractOcrEngine(string? trainedDataDirectory = null)
    {
        _trainedDataDirectory = trainedDataDirectory
            ?? Environment.GetEnvironmentVariable("DARHOUS_TESSDATA_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
        Availability = CheckAvailability(_trainedDataDirectory, ["ara"]);
    }

    public OcrEngineAvailability Availability { get; }

    public Task<OcrEngineResult> ProcessAsync(
        string inputPdfPath,
        string outputPdfPath,
        IReadOnlyList<string> languages,
        CancellationToken cancellationToken)
    {
        var availability = CheckAvailability(_trainedDataDirectory, languages);
        if (!availability.IsAvailable)
        {
            return Task.FromResult(OcrEngineResult.Failed("ocr_engine_unavailable", availability.Detail!));
        }

        try
        {
            return Task.FromResult(ProcessCore(inputPdfPath, outputPdfPath, languages, cancellationToken));
        }
        catch (Exception ex) when (IsEngineUnavailable(ex))
        {
            return Task.FromResult(OcrEngineResult.Failed(
                "ocr_engine_unavailable",
                "Tesseract native binaries or compatible language data could not be loaded."));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Task.FromResult(OcrEngineResult.Failed("ocr_failed", "Tesseract could not process this PDF."));
        }
    }

    private OcrEngineResult ProcessCore(
        string inputPdfPath,
        string outputPdfPath,
        IReadOnlyList<string> languages,
        CancellationToken cancellationToken)
    {
        var pageResults = new List<OcrPageText>();
        using (var source = PdfDocument.Open(inputPdfPath))
        using (var engine = new TesseractEngine(
                   _trainedDataDirectory,
                   string.Join('+', languages),
                   EngineMode.Default))
        {
            foreach (var page in source.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var image = SelectPrimaryImage(page);
                if (image is null)
                {
                    return OcrEngineResult.Failed(
                        "unsupported_pdf_page",
                        $"PDF page {page.Number} has no raster image that can be supplied to Tesseract without replacing the original page.");
                }

                var imageBytes = GetImageBytes(image);
                if (imageBytes.Length == 0)
                {
                    return OcrEngineResult.Failed(
                        "unsupported_pdf_page",
                        $"PDF page {page.Number}'s primary raster image could not be decoded.");
                }

                using var pix = Pix.LoadFromMemory(imageBytes);
                using var recognized = engine.Process(pix, $"page-{page.Number}");
                var words = ReadWords(recognized);
                var imageBounds = image.BoundingBox;
                pageResults.Add(new OcrPageText(
                    recognized.GetText() ?? string.Empty,
                    image.WidthInSamples,
                    image.HeightInSamples,
                    words,
                    imageBounds.Left,
                    imageBounds.Top,
                    imageBounds.Width,
                    imageBounds.Height));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        ComposeSearchablePdf(inputPdfPath, outputPdfPath, pageResults, cancellationToken);
        return new OcrEngineResult(
            string.Join(Environment.NewLine, pageResults.Select(page => page.Text)),
            outputPdfPath,
            null);
    }

    private static IPdfImage? SelectPrimaryImage(UglyToad.PdfPig.Content.Page page) =>
        page.GetImages().OrderByDescending(image => (long)image.WidthInSamples * image.HeightInSamples).FirstOrDefault();

    private static byte[] GetImageBytes(IPdfImage image)
    {
        if (image.TryGetPng(out var pngBytes)) return pngBytes;
        return image.RawBytes.ToArray();
    }

    private static IReadOnlyList<OcrWord> ReadWords(Tesseract.Page recognizedPage)
    {
        var words = new List<OcrWord>();
        using var iterator = recognizedPage.GetIterator();
        iterator.Begin();
        do
        {
            var text = iterator.GetText(PageIteratorLevel.Word);
            if (!string.IsNullOrWhiteSpace(text) && iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds))
            {
                words.Add(new OcrWord(text.Trim(), bounds.X1, bounds.Y1, bounds.Width, bounds.Height));
            }
        }
        while (iterator.Next(PageIteratorLevel.Word));

        return words;
    }

    internal static void ComposeSearchablePdf(
        string inputPdfPath,
        string outputPdfPath,
        IReadOnlyList<OcrPageText> pageResults,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows()) GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        using var source = PdfReader.Open(inputPdfPath, PdfDocumentOpenMode.Import);
        using var output = new PdfSharp.Pdf.PdfDocument();

        for (var index = 0; index < source.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = output.AddPage(source.Pages[index]);
            var recognized = pageResults[index];
            using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            var brush = new XSolidBrush(XColor.FromArgb(0, 0, 0, 0));
            var imageLeft = recognized.PdfLeft ?? 0;
            var imageTop = recognized.PdfTop is { } pdfTop ? page.Height.Point - pdfTop : 0;
            var imageWidth = recognized.PdfWidth ?? page.Width.Point;
            var imageHeight = recognized.PdfHeight ?? page.Height.Point;
            var scaleX = imageWidth / recognized.ImageWidth;
            var scaleY = imageHeight / recognized.ImageHeight;

            foreach (var word in recognized.Words)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var height = Math.Max(1, word.Height * scaleY);
                var font = new XFont("Arial", height, XFontStyleEx.Regular);
                graphics.DrawString(
                    word.Text,
                    font,
                    brush,
                    new XRect(
                        imageLeft + word.X * scaleX,
                        imageTop + word.Y * scaleY,
                        Math.Max(1, word.Width * scaleX),
                        height),
                    XStringFormats.TopLeft);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPdfPath)!);
        output.Save(outputPdfPath);
    }

    private static OcrEngineAvailability CheckAvailability(string directory, IReadOnlyList<string> languages)
    {
        if (!Directory.Exists(directory))
        {
            return new(false, $"OCR engine unavailable: trained-data directory '{directory}' does not exist.");
        }

        var missing = languages.Where(language => !File.Exists(Path.Combine(directory, $"{language}.traineddata"))).ToArray();
        return missing.Length == 0
            ? new(true)
            : new(false, $"OCR engine unavailable: missing trained data for {string.Join(", ", missing)}.");
    }

    private static bool IsEngineUnavailable(Exception exception) =>
        exception is DllNotFoundException or BadImageFormatException or TypeInitializationException or EntryPointNotFoundException ||
        exception.InnerException is not null && IsEngineUnavailable(exception.InnerException);

    public void Dispose() { }
}
