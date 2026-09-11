using System.Security.Cryptography;

namespace Darhous.Archive.Security.Sessions;

/// <summary>
/// Session tokens are high-entropy random values, not user-chosen secrets — unlike
/// passwords they don't need slow/memory-hard hashing (Argon2id) to resist brute force;
/// a fast cryptographic hash of a 256-bit random value is standard practice and keeps
/// session validation cheap (it runs on every authenticated request).
/// </summary>
public static class SessionTokens
{
    public static string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
