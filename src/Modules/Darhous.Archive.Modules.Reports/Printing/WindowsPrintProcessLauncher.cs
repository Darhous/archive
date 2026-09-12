using System.Diagnostics;

namespace Darhous.Archive.Modules.Reports.Printing;

public sealed class WindowsPrintProcessLauncher : IPrintProcessLauncher
{
    public void Launch(PrintProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("PDF shell printing is only supported on Windows.");
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = request.FileName,
            Verb = request.Verb,
            UseShellExecute = request.UseShellExecute,
        }) ?? throw new InvalidOperationException("Windows did not start the registered PDF print handler.");
    }
}
