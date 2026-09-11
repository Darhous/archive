namespace Darhous.Archive.Security.Secrets;

/// <summary>
/// Encrypts small secrets at rest (AI provider API keys, Telegram bot token — SAD §68/§75.
/// Implementation Plan §94: Windows DPAPI.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
