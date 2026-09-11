using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Darhous.Archive.Audit.Writing;
using Darhous.Archive.Core.Audit;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Persistence.Configuration;

namespace Darhous.Archive.Audit;

public static class ServiceCollectionExtensions
{
    /// <summary>Call after <c>AddPersistence</c> — this resolves the Audit-keyed <c>ISqliteWriteQueue</c> it registers.</summary>
    public static IServiceCollection AddAudit(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock, SystemClock>();

        services.AddSingleton<BufferedAuditService>(sp => new BufferedAuditService(
            sp.GetRequiredKeyedService<Persistence.Writes.ISqliteWriteQueue>(DatabaseKind.Audit),
            sp.GetRequiredService<Persistence.Connections.ISqliteConnectionFactory>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BufferedAuditService>>()));

        services.AddSingleton<IAuditService>(sp => sp.GetRequiredService<BufferedAuditService>());
        services.AddSingleton<IAuditQueryService>(sp => sp.GetRequiredService<BufferedAuditService>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<BufferedAuditService>());

        return services;
    }
}
