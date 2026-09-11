using Darhous.Archive.Security.Passwords;

namespace Darhous.Archive.Security.Tests;

public class Argon2idPasswordHasherTests
{
    private static readonly Argon2idPasswordHasher Hasher = new(
        new Argon2idOptions(MemorySizeKiB: 8 * 1024, Iterations: 2, DegreeOfParallelism: 1));

    [Fact]
    public void Hash_ProducesEncodedArgon2idString()
    {
        var hash = Hasher.Hash("correct horse battery staple");

        Assert.StartsWith("$argon2id$v=19$m=8192,t=2,p=1$", hash);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = Hasher.Hash("correct horse battery staple");

        Assert.True(Hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = Hasher.Hash("correct horse battery staple");

        Assert.False(Hasher.Verify("wrong password", hash));
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentSalts()
    {
        var first = Hasher.Hash("same-password");
        var second = Hasher.Hash("same-password");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Verify_MalformedHash_ReturnsFalse()
    {
        Assert.False(Hasher.Verify("anything", "not-a-valid-hash"));
    }
}
