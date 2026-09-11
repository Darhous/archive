using Darhous.Archive.Contracts.Audit;

namespace Darhous.Archive.Core.Audit;

/// <summary>Backs the Audit Log viewer (SAD §15) — search/filter/paginate audit_events.</summary>
public interface IAuditQueryService
{
    Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken);
}
