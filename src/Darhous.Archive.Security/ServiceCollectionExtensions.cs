using Microsoft.Extensions.DependencyInjection;
using Darhous.Archive.Core.Permissions;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Security.Authentication;
using Darhous.Archive.Security.Passwords;
using Darhous.Archive.Security.Permissions;
using Darhous.Archive.Security.Secrets;

namespace Darhous.Archive.Security;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSecurity(this IServiceCollection services, AuthenticationOptions? authenticationOptions = null)
    {
        services.AddSingleton<IPasswordHasher>(new Argon2idPasswordHasher());
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<IPermissionEvaluator, DefaultPermissionEvaluator>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(authenticationOptions ?? new AuthenticationOptions());
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IUserManagementService, UserManagementService>();

        return services;
    }
}
