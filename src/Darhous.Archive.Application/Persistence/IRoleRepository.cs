namespace Darhous.Archive.Application.Persistence;

/// <summary>
/// The one repository built in Phase 2 to prove the Unit-of-Work/write-queue pattern
/// end to end (Phase 2 design review). Document/Folder/... repositories are added by
/// their own phases, not here.
/// </summary>
public interface IRoleRepository
{
    Task<Guid> CreateAsync(string code, string displayName, bool isSystem, CancellationToken cancellationToken);

    Task<Role?> GetByUidAsync(Guid uid, CancellationToken cancellationToken);

    Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken);
}
