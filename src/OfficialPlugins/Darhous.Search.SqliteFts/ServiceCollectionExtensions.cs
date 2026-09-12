using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Writes;
using Darhous.Search.SqliteFts.Availability;
using Darhous.Search.SqliteFts.Indexing;
using Darhous.Search.SqliteFts.Query;
using Darhous.Search.SqliteFts.Rebuild;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Darhous.Search.SqliteFts;

/// <summary>
/// Official Plugin registration (Implementation Plan §5: <c>OfficialPlugins/Darhous.Search.SqliteFts</c>).
/// A full third-party Plugin Host lands in Phase 12 — until then this wires in-process
/// exactly like a Module (<c>IArchiveModule</c> pattern), reusing the Search-keyed
/// <see cref="ISqliteWriteQueue"/>/connection factory that <c>AddPersistence</c> already
/// registers generically for every <see cref="DatabaseKind"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSearchPlugin(this IServiceCollection services, SearchIndexingOptions? options = null)
    {
        services.AddSingleton(options ?? new SearchIndexingOptions());

        services.AddSingleton<ISearchAvailability, SearchAvailability>();

        services.AddSingleton<ISearchIndexWriter>(sp => new SearchIndexWriter(
            sp.GetRequiredKeyedService<ISqliteWriteQueue>(DatabaseKind.Search),
            sp.GetRequiredService<ISearchAvailability>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SearchIndexWriter>>()));

        services.AddSingleton<SearchReconciliationService>();
        services.AddHostedService(sp => sp.GetRequiredService<SearchReconciliationService>());

        services.AddSingleton<ISearchIndexRebuilder>(sp => new SearchIndexRebuilder(
            sp.GetRequiredKeyedService<ISqliteWriteQueue>(DatabaseKind.Search),
            sp.GetRequiredService<SearchReconciliationService>(),
            sp.GetRequiredService<ISearchAvailability>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SearchIndexRebuilder>>()));

        services.AddSingleton<IFtsQueryService>(sp => new FtsQueryService(
            sp.GetRequiredService<ISqliteConnectionFactory>(),
            sp.GetRequiredService<ISearchAvailability>(),
            sp.GetRequiredService<Darhous.Archive.Core.Audit.IAuditService>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FtsQueryService>>()));

        return services;
    }
}
