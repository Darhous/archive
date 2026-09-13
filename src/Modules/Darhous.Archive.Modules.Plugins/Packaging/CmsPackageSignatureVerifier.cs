using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

namespace Darhous.Archive.Modules.Plugins.Packaging;

/// <summary>
/// Shared CMS/PKCS#7 detached-signature verifier for installable Darhous packages. The
/// cryptographic check and the explicit publisher allow-list are deliberately separate.
/// </summary>
public static class CmsPackageSignatureVerifier
{
    public static IReadOnlyList<string> Verify(
        byte[] signedContent,
        byte[]? signatureBytes,
        IPluginTrustStore trustStore)
    {
        ArgumentNullException.ThrowIfNull(signedContent);
        ArgumentNullException.ThrowIfNull(trustStore);

        if (signatureBytes is null)
        {
            return ["Package is missing signature.p7s."];
        }

        var cms = new SignedCms(new ContentInfo(signedContent), detached: true);
        try
        {
            cms.Decode(signatureBytes);
        }
        catch (CryptographicException exception)
        {
            return [$"signature.p7s is not a valid PKCS#7/CMS structure: {exception.Message}"];
        }

        try
        {
            // Mathematical signature validity is checked here. Publisher trust is the
            // explicit thumbprint allow-list below, matching the Phase 12 trust model.
            cms.CheckSignature(verifySignatureOnly: true);
        }
        catch (CryptographicException exception)
        {
            return [$"Signature verification failed - signed content was changed: {exception.Message}"];
        }

        var signerCertificate = cms.SignerInfos.Count > 0 ? cms.SignerInfos[0].Certificate : null;
        if (signerCertificate is null)
        {
            return ["Signature has no signer certificate."];
        }

        if (!trustStore.IsTrusted(signerCertificate.Thumbprint))
        {
            return [$"Signer certificate '{signerCertificate.Subject}' (thumbprint {signerCertificate.Thumbprint}) is not in the trusted publisher list."];
        }

        return [];
    }
}
