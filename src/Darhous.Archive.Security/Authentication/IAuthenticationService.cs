using Darhous.Archive.Core.Results;
using Darhous.Archive.Security.Sessions;

namespace Darhous.Archive.Security.Authentication;

/// <summary>
/// SAD §14 (نظام الدخول). "Switch User" is deliberately not its own method — it is
/// Logout(currentToken) followed by Login(newCredentials), composed by the caller (the
/// Login UI), since there is nothing session-wise that a switch needs beyond that sequence.
/// </summary>
public interface IAuthenticationService
{
    Task<Result<AuthenticatedSession>> LoginAsync(
        string username, string password, bool rememberMe, CancellationToken cancellationToken);

    Task LogoutAsync(string sessionToken, CancellationToken cancellationToken);

    /// <summary>Null if the token is unknown, expired, or revoked. Touches last_seen_at on success.</summary>
    Task<ArchivePrincipal?> ValidateSessionAsync(string sessionToken, CancellationToken cancellationToken);
}
