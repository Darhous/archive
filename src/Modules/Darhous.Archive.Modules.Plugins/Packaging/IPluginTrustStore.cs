namespace Darhous.Archive.Modules.Plugins.Packaging;

/// <summary>
/// Which signer certificates are trusted to sign an installable package. Deliberately a
/// thumbprint allow-list rather than validating against the Windows certificate store's
/// normal chain-of-trust rules — a plugin signer is not expected to chain to a public root
/// CA, and requiring that would make it impossible for a developer/verified publisher to
/// ever be trusted without buying a commercial code-signing certificate.
/// </summary>
public interface IPluginTrustStore
{
    bool IsTrusted(string certificateThumbprint);

    void Trust(string certificateThumbprint);

    void Revoke(string certificateThumbprint);
}
