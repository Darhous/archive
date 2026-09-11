using Darhous.Archive.Contracts.Audit;

namespace Darhous.Archive.Core.Audit;

/// <summary>
/// Every module logs through this rather than writing to audit.db directly. Implemented by
/// the Audit project (Phase 4) — Critical actions (DB Spec §106) are written immediately and
/// synchronously (the caller awaits the actual commit); everything else is buffered and
/// flushed periodically for throughput, per Implementation Plan §11 (Audit Project:
/// "Audit buffering" / "Critical event immediate write").
/// </summary>
public interface IAuditService
{
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken);
}
