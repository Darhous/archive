using Darhous.Archive.Security.Sessions;

namespace Darhous.Archive.Security.Authentication;

/// <summary>
/// The raw <see cref="SessionToken"/> is only ever available here, right after login —
/// only its hash is ever persisted (DB Spec §21).
/// </summary>
public sealed record AuthenticatedSession(string SessionToken, ArchivePrincipal Principal, DateTimeOffset ExpiresAt);
