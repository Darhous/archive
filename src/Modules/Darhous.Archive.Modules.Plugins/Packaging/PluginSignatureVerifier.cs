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

        return CmsPackageSignatureVerifier.Verify(
            Encoding.UTF8.GetBytes(checksumsJson), package.SignatureBytes, trustStore);
    }
}
