using Darhous.Archive.Modules.Importers;
using Darhous.Archive.Modules.Importers.Extractors;

namespace Darhous.Archive.Modules.Importers.Tests;

public class ExtractorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "darhous-importer-tests", Guid.NewGuid().ToString("N"));

    public ExtractorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void PdfContentExtractor_CanHandle_OnlyPdf()
    {
        var extractor = new PdfContentExtractor();
        Assert.True(extractor.CanHandle(".pdf"));
        Assert.True(extractor.CanHandle(".PDF"));
        Assert.False(extractor.CanHandle(".docx"));
    }

    [Fact]
    public async Task PdfContentExtractor_TextLayerPresent_ExtractsTextAndMarksSearchable()
    {
        var path = SampleFiles.CreateSimplePdf(_dir, "Hello real PdfPig text layer content");
        var extractor = new PdfContentExtractor();

        var result = await extractor.ExtractAsync(path, CancellationToken.None);

        Assert.True(result.IsSearchablePdf);
        Assert.Contains("Hello real PdfPig", result.Text);
        Assert.Equal(1, result.PageCount);
    }

    [Fact]
    public async Task PdfContentExtractor_NoTextLayer_MarksNotSearchable()
    {
        var path = SampleFiles.CreateImageOnlyPdf(_dir);
        var extractor = new PdfContentExtractor();

        var result = await extractor.ExtractAsync(path, CancellationToken.None);

        Assert.False(result.IsSearchablePdf);
        Assert.Null(result.Text);
    }

    [Fact]
    public void OpenXmlContentExtractor_CanHandle_OfficeFormatsOnly()
    {
        var extractor = new OpenXmlContentExtractor();
        Assert.True(extractor.CanHandle(".docx"));
        Assert.True(extractor.CanHandle(".xlsx"));
        Assert.True(extractor.CanHandle(".pptx"));
        Assert.False(extractor.CanHandle(".doc"));
        Assert.False(extractor.CanHandle(".pdf"));
    }

    [Fact]
    public async Task OpenXmlContentExtractor_Docx_ExtractsParagraphText()
    {
        var path = SampleFiles.CreateDocx(_dir, "محتوى تجريبي داخل Word");
        var extractor = new OpenXmlContentExtractor();

        var result = await extractor.ExtractAsync(path, CancellationToken.None);

        Assert.Contains("محتوى تجريبي داخل Word", result.Text);
        Assert.Null(result.PageCount);
    }

    [Fact]
    public async Task OpenXmlContentExtractor_Pptx_ExtractsSlideTextAndCountsSlides()
    {
        var path = SampleFiles.CreatePptx(_dir, "عنوان الشريحة الأولى");
        var extractor = new OpenXmlContentExtractor();

        var result = await extractor.ExtractAsync(path, CancellationToken.None);

        Assert.Contains("عنوان الشريحة الأولى", result.Text);
        Assert.Equal(1, result.PageCount);
    }

    [Fact]
    public async Task OpenXmlContentExtractor_Xlsx_ExtractsCellText()
    {
        var path = SampleFiles.CreateXlsx(_dir, "بيانات الخلية");
        var extractor = new OpenXmlContentExtractor();

        var result = await extractor.ExtractAsync(path, CancellationToken.None);

        Assert.Contains("بيانات الخلية", result.Text);
    }

    [Fact]
    public void EmlContentExtractor_CanHandle_OnlyEml()
    {
        var extractor = new EmlContentExtractor();
        Assert.True(extractor.CanHandle(".eml"));
        Assert.False(extractor.CanHandle(".msg"));
    }

    [Fact]
    public async Task EmlContentExtractor_ExtractsHeadersAndBody()
    {
        var path = SampleFiles.CreateEml(_dir, "اجتماع غدًا", "ahmed@example.com", "sara@example.com", "من فضلك احضري الاجتماع الساعة 10.");
        var extractor = new EmlContentExtractor();

        var result = await extractor.ExtractAsync(path, CancellationToken.None);

        Assert.Contains("الاجتماع", result.Text);
        Assert.Contains("اجتماع غدًا", result.MetadataJson);
        Assert.Contains("ahmed@example.com", result.MetadataJson);
    }

    [Fact]
    public void MsgContentExtractor_CanHandle_OnlyMsg()
    {
        var extractor = new MsgContentExtractor();
        Assert.True(extractor.CanHandle(".msg"));
        Assert.False(extractor.CanHandle(".eml"));
    }
}
