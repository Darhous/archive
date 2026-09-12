using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;

namespace Darhous.Archive.Modules.Plugins.Packaging;

/// <summary>
/// Plugin SDK §38 ("Validate signature", "Validate publisher") — CMS/PKCS#7 (decision log:
/// "CMS/PKCS#7 X.509 package signing"). The signed content is <c>checksums.json</c>'s raw
/// bytes, not every file individually — since every payload file's hash is already inside
/// checksums.json, signing that one file transitively covers the whole package's integrity.
/// A cryptographically valid signature only proves "this checksums.json was signed by this
/// certificate" — whether that certificate is one we actually trust is a separate check
/// against <see cref="IPluginTrustStore"/>, deliberately kept apart from the crypto check so
/// each failure reason is unambiguous to whoever reads the validation errors.
/// </summary>
public static class PluginSignatureVerifier
{
    public static IReadOnlyList<string> Verify(PluginPackage package, IPluginTrustStore trustStore)
    {
        var checksumsJson = package.RawChecksumsJson;
        if (checksumsJson is null)
        {
            return ["Cannot verify signature — checksums.json is missing."];
        }

        var signatureBytes = package.SignatureBytes;
        if (signatureBytes is null)
        {
            return ["Package is missing signature.p7s."];
        }

        var signedContent = new ContentInfo(Encoding.UTF8.GetBytes(checksumsJson));
        var cms = new SignedCms(signedContent, detached: true);

        try
        {
            cms.Decode(signatureBytes);
        }
        catch (CryptographicException ex)
        {
            return [$"signature.p7s is not a valid PKCS#7/CMS structure: {ex.Message}"];
        }

        try
        {
            // verifySignatureOnly: true — validate the cryptographic signature without also
            // requiring the signer certificate to chain to a trusted root in the OS store;
            // trust is decided explicitly below via IPluginTrustStore instead.
            cms.CheckSignature(verifySignatureOnly: true);
        }
        catch (CryptographicException ex)
        {
            return [$"Signature verification failed — checksums.json does not match the signed content: {ex.Message}"];
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
