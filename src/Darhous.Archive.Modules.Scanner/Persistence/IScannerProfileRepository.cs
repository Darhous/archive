using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Darhous.Archive.Modules.Scanner.Persistence;

public interface IScannerProfileRepository
{
    Task<IReadOnlyList<ScannerProfile>> ListAllAsync(CancellationToken cancellationToken);
}
