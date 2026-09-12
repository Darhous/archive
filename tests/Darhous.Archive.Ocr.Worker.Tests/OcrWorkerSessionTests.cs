using Darhous.Archive.Ocr.Worker;
using Darhous.Archive.Workers;
using Darhous.Archive.Workers.Protocol;
using Darhous.Archive.Workers.Transport;
using PdfSharp.Drawing;
using UglyToad.PdfPig;

namespace Darhous.Archive.Ocr.Worker.Tests;

public sealed class OcrWorkerSessionTests
{
    [Fact]
    public async Task Request_ReturnsTextAndManagedSearchablePdf()
    {
        using var engine = new FakeOcrEngine();
        await using var connection = await Connection.OpenAsync(engine);
        var response = await connection.SendAsync(["ara", "eng"]);

        Assert.Equal(OcrMessageTypes.Result, response.MessageType);
        Assert.Equal("request-1", response.CorrelationId);
        var result = response.DeserializePayload<OcrSuccess>();
        Assert.NotNull(result);
        Assert.Equal("نص تجريبي sample", result.Text);
        Assert.Equal(["ara", "eng"], engine.LastLanguages);
        Assert.True(File.Exists(result.SearchablePdf.Path));
        Assert.True(new ManagedTempPathValidator(connection.Options).IsWithinManagedTempRoot(result.SearchablePdf.Path));
    }

    [Fact]
    public async Task EngineUnavailable_ReturnsExplicitFailureAndSessionRemainsUsable()
    {
        using var engine = new FakeOcrEngine
        {
            Availability = new(false, "No trained data."),
            Failure = new("ocr_engine_unavailable", "No trained data."),
        };
        await using var connection = await Connection.OpenAsync(engine);
        Assert.Equal("degraded", connection.Health.Status);

        var failed = await connection.SendAsync(["ara"]);
        Assert.Equal("ocr_engine_unavailable", failed.DeserializePayload<OcrFailure>()!.Code);

        engine.Failure = null;
        var succeeded = await connection.SendAsync(["ara"]);
        Assert.NotNull(succeeded.DeserializePayload<OcrSuccess>());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[\"fra\"]")]
    [InlineData("[\"ara\",\"ara\"]")]
    [InlineData("[\"ARA\"]")]
    public async Task InvalidLanguages_DoNotCallEngine(string languagesJson)
    {
        using var engine = new FakeOcrEngine();
        await using var connection = await Connection.OpenAsync(engine);
        using var languages = System.Text.Json.JsonDocument.Parse(languagesJson);
        var input = connection.CreateInput();
        using var payload = System.Text.Json.JsonDocument.Parse(
            $"{{\"Input\":{{\"Path\":{System.Text.Json.JsonSerializer.Serialize(input)}}},\"Languages\":{languages.RootElement.GetRawText()}}}");
        await connection.Host.SendAsync(
            WorkerMessage.Create(1, OcrMessageTypes.Request, payload.RootElement),
            connection.Timeout.Token);

        var response = await connection.Host.ReceiveAsync(connection.Timeout.Token);
        Assert.Equal("invalid_request", response.DeserializePayload<OcrFailure>()!.Code);
        Assert.Equal(0, engine.CallCount);
    }

    [Fact]
    public async Task EscapingInputPath_IsRejectedBeforeEngineCall()
    {
        using var engine = new FakeOcrEngine();
        await using var connection = await Connection.OpenAsync(engine);
        var request = new OcrRequest(new LargeDataReference(Path.Combine(connection.Options.ManagedTempStorageRoot, "..", "outside.pdf")), ["ara"]);
        await connection.Host.SendAsync(WorkerMessage.Create(1, OcrMessageTypes.Request, request), connection.Timeout.Token);

        var response = await connection.Host.ReceiveAsync(connection.Timeout.Token);
        Assert.Equal("invalid_request", response.DeserializePayload<OcrFailure>()!.Code);
        Assert.Equal(0, engine.CallCount);
    }

    [Theory]
    [InlineData("other", 1)]
    [InlineData("ocr.request", 2)]
    public async Task UnsupportedEnvelope_ReturnsProtocolErrorAndContinues(string messageType, int version)
    {
        using var engine = new FakeOcrEngine();
        await using var connection = await Connection.OpenAsync(engine);
        await connection.Host.SendAsync(WorkerMessage.Create(version, messageType, new { }), connection.Timeout.Token);

        var response = await connection.Host.ReceiveAsync(connection.Timeout.Token);
        Assert.Equal(OcrMessageTypes.ProtocolError, response.MessageType);
        Assert.NotNull((await connection.SendAsync(["ara"])).DeserializePayload<OcrSuccess>());
    }

    [Fact]
    public async Task HostDisconnect_EndsSession()
    {
        using var engine = new FakeOcrEngine();
        await using var connection = await Connection.OpenAsync(engine);
        await connection.Host.DisposeAsync();
        await connection.Running.WaitAsync(connection.Timeout.Token);
    }

    [Fact]
    public void TesseractEngine_MissingTrainedData_IsGracefullyUnavailable()
    {
        using var engine = new TesseractOcrEngine(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.False(engine.Availability.IsAvailable);
        Assert.Contains("unavailable", engine.Availability.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PdfComposition_ImportsOriginalPageAndAddsSearchableTransparentText()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Darhous.Ocr.Composition.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var original = Path.Combine(directory, "original.pdf");
        var derived = Path.Combine(directory, "derived.pdf");
        using (var pdf = new PdfSharp.Pdf.PdfDocument())
        {
            var page = pdf.AddPage();
            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawRectangle(XBrushes.Black, 10, 10, 100, 50);
            pdf.Save(original);
        }

        TesseractOcrEngine.ComposeSearchablePdf(
            original,
            derived,
            [new OcrPageText("searchable", 1000, 1000, [new OcrWord("searchable", 100, 100, 300, 50)])],
            CancellationToken.None);

        using var originalPdf = PdfDocument.Open(original);
        using var derivedPdf = PdfDocument.Open(derived);
        Assert.Equal(originalPdf.NumberOfPages, derivedPdf.NumberOfPages);
        Assert.Equal(originalPdf.GetPage(1).Width, derivedPdf.GetPage(1).Width);
        Assert.Contains("searchable", derivedPdf.GetPage(1).Text, StringComparison.OrdinalIgnoreCase);
        Directory.Delete(directory, recursive: true);
    }

    private sealed class FakeOcrEngine : IOcrEngine
    {
        public OcrEngineAvailability Availability { get; set; } = new(true);
        public OcrFailure? Failure { get; set; }
        public IReadOnlyList<string>? LastLanguages { get; private set; }
        public int CallCount { get; private set; }

        public async Task<OcrEngineResult> ProcessAsync(
            string inputPdfPath,
            string outputPdfPath,
            IReadOnlyList<string> languages,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastLanguages = languages.ToArray();
            if (Failure is not null) return new(null, null, Failure);
            await File.WriteAllTextAsync(outputPdfPath, "%PDF-1.4 fake searchable PDF", cancellationToken);
            return new("نص تجريبي sample", outputPdfPath, null);
        }

        public void Dispose() { }
    }

    private sealed class Connection : IAsyncDisposable
    {
        public WorkerProtocolOptions Options { get; } = new()
        {
            ManagedTempStorageRoot = Path.Combine(Path.GetTempPath(), "Darhous.Ocr.Worker.Tests", Guid.NewGuid().ToString("N")),
        };
        public CancellationTokenSource Timeout { get; } = new(TimeSpan.FromSeconds(30));
        public NamedPipeTransport Host { get; private set; } = null!;
        private NamedPipeTransport _worker = null!;
        public Task Running { get; private set; } = null!;
        public WorkerHealthPayload Health { get; private set; } = null!;

        public static async Task<Connection> OpenAsync(IOcrEngine engine)
        {
            var connection = new Connection();
            Directory.CreateDirectory(connection.Options.ManagedTempStorageRoot);
            var pipeName = NamedPipeTransport.CreateUniquePipeName(connection.Options);
            connection.Host = NamedPipeTransport.CreateServer(pipeName, connection.Options);
            var clientTask = NamedPipeTransport.ConnectClientAsync(pipeName, connection.Options, connection.Timeout.Token);
            await Task.WhenAll(connection.Host.WaitForConnectionAsync(connection.Timeout.Token), clientTask);
            connection._worker = await clientTask;

            var handshake = new WorkerHandshakeHost(connection.Options);
            connection.Running = new OcrWorkerSession(engine, connection.Options)
                .RunAsync(connection._worker, handshake.SessionToken, connection.Timeout.Token);
            var description = await handshake.AcceptAsync(
                connection.Host,
                OcrWorkerSession.WorkerId,
                connection.Timeout.Token);
            connection.Health = description.Health;
            Assert.Equal(OcrWorkerSession.Capabilities, description.Capabilities);
            return connection;
        }

        public string CreateInput()
        {
            var path = Path.Combine(Options.ManagedTempStorageRoot, $"input-{Guid.NewGuid():N}.pdf");
            File.WriteAllText(path, "%PDF-1.4 fake input");
            return path;
        }

        public async Task<WorkerMessage> SendAsync(IReadOnlyList<string> languages)
        {
            var request = new OcrRequest(new LargeDataReference(CreateInput()), languages);
            await Host.SendAsync(
                WorkerMessage.Create(1, OcrMessageTypes.Request, request, requestId: "request-1"),
                Timeout.Token);
            return await Host.ReceiveAsync(Timeout.Token);
        }

        public async ValueTask DisposeAsync()
        {
            await Timeout.CancelAsync();
            try { await Running; } catch (OperationCanceledException) { }
            await Host.DisposeAsync();
            await _worker.DisposeAsync();
            Timeout.Dispose();
            if (Directory.Exists(Options.ManagedTempStorageRoot)) Directory.Delete(Options.ManagedTempStorageRoot, recursive: true);
        }
    }
}
