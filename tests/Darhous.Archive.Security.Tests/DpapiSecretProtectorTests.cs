using Darhous.Archive.Security.Secrets;

namespace Darhous.Archive.Security.Tests;

public class DpapiSecretProtectorTests
{
    private readonly DpapiSecretProtector _protector = new();

    [Fact]
    public void Protect_ThenUnprotect_RoundTrips()
    {
        var protectedValue = _protector.Protect("my-secret-api-key");

        Assert.Equal("my-secret-api-key", _protector.Unprotect(protectedValue));
    }

    [Fact]
    public void Protect_DoesNotLeakPlaintext()
    {
        var protectedValue = _protector.Protect("my-secret-api-key");

        Assert.DoesNotContain("my-secret-api-key", protectedValue);
    }
}
