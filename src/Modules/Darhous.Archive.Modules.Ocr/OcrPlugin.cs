using Darhous.Archive.Core.Health;
using Darhous.Archive.Core.Time;
using Darhous.Archive.PluginSdk;
using Darhous.Archive.PluginSdk.Configuration;
using Darhous.Archive.PluginSdk.Runtime;
using Darhous.Archive.Workers.Host;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Modules.Ocr;

/// <summary>Official out-of-process OCR plugin lifecycle.</summary>
public sealed class OcrPlugin : IArchivePlugin
{
    private readonly OcrSupervisionOptions _options;
    private IManagedOcrProvider? _provider;

    public OcrPlugin() : this(new OcrSupervisionOptions()) { }

    internal OcrPlugin(OcrSupervisionOptions options) => _options = options;

    internal OcrPlugin(IManagedOcrProvider provider)
    {
        _options = new OcrSupervisionOptions();
        _provider = provider;
    }

    public PluginIdentity Identity { get; } =
        new("Darhous.Ocr.Tesseract", new Version(1, 0, 0), "Darhous", "Tesseract OCR");

    public ValueTask ConfigureAsync(IPluginConfigurationContext context, CancellationToken cancellationToken)
    {
        _provider ??= new WorkerOcrProvider(
            _options, context.Health, new SystemClock(), new DefaultWorkerDelayProvider(), NullLoggerFactory.Instance);
        context.Services.AddSingleton<IOcrProvider>(_provider);
        return ValueTask.CompletedTask;
    }

    public async ValueTask StartAsync(IPluginRuntimeContext context, CancellationToken cancellationToken)
    {
        if (_provider is null) throw new InvalidOperationException("OCR plugin must be configured before it starts.");
        await _provider.StartAsync(cancellationToken);
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        if (_provider is not null) await _provider.StopAsync(cancellationToken);
    }
}
