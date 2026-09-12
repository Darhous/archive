using System.Threading;
using System.Threading.Tasks;

namespace Darhous.Archive.Modules.Scanner;

public interface IScannerProvider
{
    Task<ScannerHealthStatus> GetStatusAsync(CancellationToken cancellationToken);
    Task<ScanResult> ScanAsync(ScannerProfile profile, CancellationToken cancellationToken);
}
