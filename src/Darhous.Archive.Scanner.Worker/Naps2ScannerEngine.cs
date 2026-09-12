using NAPS2.Images;
using NAPS2.Pdf;
using NAPS2.Scan;

namespace Darhous.Archive.Scanner.Worker;

[System.Runtime.Versioning.RequiresPreviewFeatures("NAPS2.Sdk 1.3.0 marks its SDK API as preview.")]
public sealed class Naps2ScannerEngine : IScannerEngine
{
    private readonly ScanningContext _context = new(new WindowsImageContext());
    private readonly Func<CancellationToken, Task<List<ScanDevice>>>? _findDevices;
    private readonly Func<ScanOptions, CancellationToken, IAsyncEnumerable<ProcessedImage>>? _scan;

    public Naps2ScannerEngine() { }

    // Tests replace acquisition only; mapping, grouping and PDF export remain real.
    internal Naps2ScannerEngine(Func<CancellationToken, Task<List<ScanDevice>>> findDevices,
        Func<ScanOptions, CancellationToken, IAsyncEnumerable<ProcessedImage>> scan)
    {
        _findDevices = findDevices;
        _scan = scan;
    }

    public async Task<IReadOnlyList<ScannerDevice>> GetDevicesAsync(CancellationToken cancellationToken) =>
        (await FindDevicesAsync(cancellationToken)).Select(d => new ScannerDevice(Key(d), d.Name)).ToArray();

    private async Task<List<ScanDevice>> FindDevicesAsync(CancellationToken cancellationToken)
    {
        if (_findDevices is not null) return await _findDevices(cancellationToken);
        var devices = new List<ScanDevice>();
        var controller = new ScanController(_context);
        foreach (var driver in new[] { Driver.Wia, Driver.Twain })
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var options = new ScanOptions { Driver = driver };
                options.TwainOptions.Dsm = Environment.Is64BitProcess ? TwainDsm.NewX64 : TwainDsm.Old;
                await foreach (var device in controller.GetDevices(options, cancellationToken)) devices.Add(device);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { /* Missing driver/DSM is an expected degraded environment. */ }
        }
        return devices;
    }

    private static string Key(ScanDevice device) => $"{device.Driver}:{device.ID}";

    internal static ScanOptions MapOptions(ScanProfile profile, ScanDevice device)
    {
        profile.Validate();
        return new ScanOptions
        {
            Device = device,
            Dpi = profile.Resolution,
            BitDepth = profile.ColorMode switch
            {
                ScanColorMode.Color => BitDepth.Color,
                ScanColorMode.Grayscale => BitDepth.Grayscale,
                _ => BitDepth.BlackAndWhite,
            },
            PaperSource = profile.Source == ScanSource.Flatbed ? PaperSource.Flatbed
                : profile.Duplex ? PaperSource.Duplex : PaperSource.Feeder,
            PageSize = profile.PageSize switch
            {
                ScanPaperSize.Letter => PageSize.Letter,
                ScanPaperSize.Legal => PageSize.Legal,
                _ => PageSize.A4,
            },
            UseNativeUI = false,
            MaxQuality = true,
            TwainOptions = new() { Dsm = Environment.Is64BitProcess ? TwainDsm.NewX64 : TwainDsm.Old },
        };
    }

    public async Task<ScanBatch> ScanAsync(ScanProfile profile, string outputDirectory, CancellationToken cancellationToken)
    {
        profile.Validate();
        var pages = new List<ProcessedImage>();
        try
        {
            var devices = await FindDevicesAsync(cancellationToken);
            var device = profile.DeviceId is null ? devices.FirstOrDefault() : devices.Find(d => Key(d) == profile.DeviceId);
            if (device is null) return ScanBatch.Failed("no_scanner", "No scanner available for the requested device.");
            _context.TempFolderPath = outputDirectory;
            var files = new List<string>();
            var controller = new ScanController(_context);
            var scanOptions = MapOptions(profile, device);
            var acquisition = _scan is null ? controller.Scan(scanOptions, cancellationToken) : _scan(scanOptions, cancellationToken);
            await foreach (var page in acquisition)
            {
                pages.Add(page);
                if (pages.Count == profile.GroupSize) await ExportGroupAsync();
            }
            if (pages.Count > 0) await ExportGroupAsync();
            return files.Count == 0 ? ScanBatch.Failed("no_pages", "The scanner returned no pages.") : new(files);

            async Task ExportGroupAsync()
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Path.Combine(outputDirectory, $"document-{files.Count + 1:D4}.pdf");
                if (!await new PdfExporter(_context).Export(path, pages)) throw new IOException("PDF export failed.");
                cancellationToken.ThrowIfCancellationRequested();
                files.Add(path);
                foreach (var image in pages) image.Dispose();
                pages.Clear();
            }
        }
        catch (OperationCanceledException) { return ScanBatch.Failed("cancelled", "Scan cancelled."); }
        catch (Exception) { return ScanBatch.Failed("device_error", "Scanner acquisition or PDF export failed."); }
        finally { foreach (var page in pages) page.Dispose(); }
    }

    public void Dispose() => _context.Dispose();
}
