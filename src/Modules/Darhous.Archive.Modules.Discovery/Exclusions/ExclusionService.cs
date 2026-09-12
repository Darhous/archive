using Darhous.Archive.Application.Persistence;

namespace Darhous.Archive.Modules.Discovery.Exclusions;

public sealed class ExclusionService(IUnitOfWork unitOfWork) : IExclusionService
{
    public async Task SeedTechnicalExclusionsAsync(CancellationToken cancellationToken)
    {
        foreach (var root in DiscoveryDefaults.GetTechnicalExclusionRoots())
        {
            await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
            {
                await context.SourceExclusions.CreateIfMissingAsync(
                    new NewSourceExclusion("folder", root, IsSystem: true, Reason: "Technical exclusion (Implementation Plan §48)", CreatedBy: null), ct);
                return null;
            }, cancellationToken);
        }
    }

    public Task<Guid> AddUserExclusionAsync(string exclusionType, string path, string? reason, Guid? createdBy, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(
            (context, ct) => context.SourceExclusions.CreateIfMissingAsync(new NewSourceExclusion(exclusionType, path, IsSystem: false, reason, createdBy), ct),
            cancellationToken);

    public async Task<ExclusionSet> LoadActiveExclusionsAsync(CancellationToken cancellationToken)
    {
        var exclusions = await unitOfWork.ExecuteAsync((context, ct) => context.SourceExclusions.ListEnabledAsync(ct), cancellationToken);
        var normalizedPrefixes = exclusions.Select(e => e.PathNormalized).ToList();

        return new ExclusionSet(normalizedPrefixes, DiscoveryDefaults.ExcludedDirectoryNames);
    }
}
