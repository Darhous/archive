using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Audit;
using Darhous.Archive.Core.Audit;
using Darhous.Archive.Core.Results;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Security.Passwords;
using Darhous.Archive.Security.Sessions;

namespace Darhous.Archive.Security.Authentication;

public sealed class AuthenticationService(
    IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, IClock clock, IAuditService auditService,
    AuthenticationOptions? options = null)
    : IAuthenticationService
{
    private readonly AuthenticationOptions _options = options ?? new AuthenticationOptions();

    public async Task<Result<AuthenticatedSession>> LoginAsync(
        string username, string password, bool rememberMe, CancellationToken cancellationToken)
    {
        var result = await unitOfWork.ExecuteAsync(async (context, ct) =>
        {
            var user = await context.Users.GetByUsernameAsync(username, ct);
            var now = clock.UtcNow;

            // Same generic error for "no such user" and "wrong password" — never reveal
            // which one it was (standard credential-enumeration defense).
            var invalidCredentials = Result<AuthenticatedSession>.Failure(
                Error.Of("AUTH_INVALID_CREDENTIALS", "اسم المستخدم أو كلمة المرور غير صحيحة."));

            if (user is null || !user.IsActive)
            {
                return invalidCredentials;
            }

            if (user.LockedUntil is { } lockedUntil && lockedUntil > now)
            {
                return Result<AuthenticatedSession>.Failure(
                    Error.Of("AUTH_ACCOUNT_LOCKED", $"الحساب مقفل مؤقتًا حتى {lockedUntil:u}."));
            }

            if (!passwordHasher.Verify(password, user.PasswordHash))
            {
                var failedCount = user.FailedLoginCount + 1;
                DateTimeOffset? lockUntil = failedCount >= _options.MaxFailedAttempts
                    ? now.AddMinutes(_options.LockoutMinutes)
                    : null;

                await context.Users.RecordLoginFailureAsync(user.Uid, lockUntil, ct);
                return invalidCredentials;
            }

            await context.Users.RecordLoginSuccessAsync(user.Uid, now, ct);

            var rawToken = SessionTokens.GenerateToken();
            var expiresAt = rememberMe ? now.AddDays(_options.RememberMeDays) : now.AddHours(_options.SessionHours);
            await context.Sessions.CreateAsync(user.Uid, SessionTokens.Hash(rawToken), rememberMe, expiresAt, ct);

            var principal = new ArchivePrincipal(user.Uid, user.DisplayName, user.Role);
            return Result<AuthenticatedSession>.Success(new AuthenticatedSession(rawToken, principal, expiresAt));
        }, cancellationToken);

        // Audit lives in a separate database (audit.db) — it can never be part of the same
        // transaction as the archive.db write above (Phase 2 design review: SQLite cannot
        // commit atomically across files), so it's recorded right after, not inside the UoW.
        await auditService.RecordAsync(
            new AuditEntry(
                AuditAction.Login, AuditActionCategory.Session,
                result.IsSuccess ? AuditResult.Success : AuditResult.Failure,
                UsernameSnapshot: username,
                ErrorCode: result.IsFailure ? result.Error!.Code : null),
            cancellationToken);

        return result;
    }

    public async Task LogoutAsync(string sessionToken, CancellationToken cancellationToken)
    {
        var principal = await ValidateSessionAsync(sessionToken, cancellationToken);

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            var session = await context.Sessions.GetByTokenHashAsync(SessionTokens.Hash(sessionToken), ct);
            if (session is not null)
            {
                await context.Sessions.RevokeAsync(session.Uid, clock.UtcNow, ct);
            }

            return null;
        }, cancellationToken);

        await auditService.RecordAsync(
            new AuditEntry(
                AuditAction.Logout, AuditActionCategory.Session, AuditResult.Success,
                UserId: principal?.UserId, UsernameSnapshot: principal?.DisplayName,
                RoleSnapshot: principal?.Role.ToString()),
            cancellationToken);
    }

    // Runs through the write queue (touches last_seen_at) rather than a plain read — simplest
    // correct option for Phase 3. If session validation frequency ever becomes a bottleneck,
    // debouncing the last_seen_at touch is the first thing to optimize, not this method's shape.
    public Task<ArchivePrincipal?> ValidateSessionAsync(string sessionToken, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async (context, ct) =>
        {
            var session = await context.Sessions.GetByTokenHashAsync(SessionTokens.Hash(sessionToken), ct);
            var now = clock.UtcNow;

            if (session is null || !session.IsActive(now))
            {
                return (ArchivePrincipal?)null;
            }

            var user = await context.Users.GetByUidAsync(session.UserUid, ct);
            if (user is null || !user.IsActive)
            {
                return null;
            }

            await context.Sessions.TouchAsync(session.Uid, now, ct);
            return new ArchivePrincipal(user.Uid, user.DisplayName, user.Role);
        }, cancellationToken);
}
