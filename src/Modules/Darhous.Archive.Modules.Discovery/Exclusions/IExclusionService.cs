namespace Darhous.Archive.Modules.Discovery.Exclusions;

public interface IExclusionService
{
    /// <summary>Idempotent — safe to call on every app startup. Seeds the default technical exclusions (Implementation Plan §48) if they aren't already present.</summary>
    Task SeedTechnicalExclusionsAsync(CancellationToken cancellationToken);

    /// <summary>Adds a user exclusion (Implementation Plan §49) — drive, folder, or subfolder.</summary>
    Task<Guid> AddUserExclusionAsync(string exclusionType, string path, string? reason, Guid? createdBy, CancellationToken cancellationToken);

    /// <summary>Loads every enabled exclusion (technical + user) as an immutable snapshot for one scan.</summary>
    Task<ExclusionSet> LoadActiveExclusionsAsync(CancellationToken cancellationToken);
}
