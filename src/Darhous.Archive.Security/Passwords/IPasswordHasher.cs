namespace Darhous.Archive.Security.Passwords;

/// <summary>DB Spec (app_users) — passwords are never stored as plain text.</summary>
public interface IPasswordHasher
{
    /// <summary>Produces a self-describing encoded hash (algorithm + parameters + salt + hash).</summary>
    string Hash(string password);

    bool Verify(string password, string encodedHash);
}
