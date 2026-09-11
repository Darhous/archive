using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Darhous.Archive.Security.Passwords;

/// <summary>
/// DB Spec (app_users, Passwords section) — Argon2id via Konscious.Security.Cryptography.Argon2,
/// default parameters MemorySize=64MB, Iterations=3, DegreeOfParallelism=2.
/// Encoded format: $argon2id$v=19$m={memoryKiB},t={iterations},p={parallelism}${saltBase64}${hashBase64}
/// </summary>
public sealed class Argon2idPasswordHasher(Argon2idOptions? options = null) : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private readonly Argon2idOptions _options = options ?? Argon2idOptions.Default;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt, _options);

        return Encode(_options, salt, hash);
    }

    public bool Verify(string password, string encodedHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(encodedHash);

        if (!TryDecode(encodedHash, out var options, out var salt, out var expectedHash))
        {
            return false;
        }

        var actualHash = ComputeHash(password, salt, options);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] ComputeHash(string password, byte[] salt, Argon2idOptions options)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = options.DegreeOfParallelism,
            MemorySize = options.MemorySizeKiB,
            Iterations = options.Iterations,
        };

        return argon2.GetBytes(HashSize);
    }

    private static string Encode(Argon2idOptions options, byte[] salt, byte[] hash) =>
        $"$argon2id$v=19$m={options.MemorySizeKiB},t={options.Iterations},p={options.DegreeOfParallelism}" +
        $"${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";

    private static bool TryDecode(string encoded, out Argon2idOptions options, out byte[] salt, out byte[] hash)
    {
        options = Argon2idOptions.Default;
        salt = [];
        hash = [];

        // "", "argon2id", "v=19", "m=..,t=..,p=..", "<salt>", "<hash>"
        var parts = encoded.Split('$', StringSplitOptions.None);
        if (parts.Length != 6 || parts[1] != "argon2id")
        {
            return false;
        }

        var parameters = parts[3].Split(',');
        if (parameters.Length != 3)
        {
            return false;
        }

        try
        {
            var memoryKiB = int.Parse(parameters[0].Split('=')[1]);
            var iterations = int.Parse(parameters[1].Split('=')[1]);
            var parallelism = int.Parse(parameters[2].Split('=')[1]);

            options = new Argon2idOptions(memoryKiB, iterations, parallelism);
            salt = Convert.FromBase64String(parts[4]);
            hash = Convert.FromBase64String(parts[5]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>
/// Defaults per the decision log: MemorySize=64MB, Iterations=3, DegreeOfParallelism=2.
/// Kept adjustable (app_settings) without a code change, as noted in the master doc.
/// </summary>
public sealed record Argon2idOptions(int MemorySizeKiB, int Iterations, int DegreeOfParallelism)
{
    public static Argon2idOptions Default { get; } = new(MemorySizeKiB: 64 * 1024, Iterations: 3, DegreeOfParallelism: 2);
}
