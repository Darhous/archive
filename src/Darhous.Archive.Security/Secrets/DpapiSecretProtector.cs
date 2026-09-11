using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Darhous.Archive.Security.Secrets;

/// <summary>
/// DPAPI-backed <see cref="ISecretProtector"/>. Scoped to the local machine (not the
/// current user) because the app runs unattended background workers under the machine
/// context — a secret encrypted this way can be decrypted by any process on this machine,
/// which is the same trust boundary the app already assumes for %ProgramData%.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = "DarhousSmartArchive.SecretProtector.v1"u8.ToArray();

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext), Entropy, DataProtectionScope.LocalMachine);

        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(protectedValue);

        var plaintextBytes = ProtectedData.Unprotect(
            Convert.FromBase64String(protectedValue), Entropy, DataProtectionScope.LocalMachine);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
