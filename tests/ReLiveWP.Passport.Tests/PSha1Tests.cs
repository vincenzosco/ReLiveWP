using System.Security.Cryptography;
using System.Text;

namespace ReLiveWP.Passport.Tests;

public class PSha1Tests
{
    private static readonly byte[] Secret = Encoding.ASCII.GetBytes("secret");
    private static readonly byte[] Seed = Encoding.ASCII.GetBytes("seed");

    [Fact]
    public void FirstBlockIsHmacOfA1AndSeed()
    {
        var a1 = HMACSHA1.HashData(Secret, Seed);
        byte[] firstInput = [.. a1, .. Seed];
        var expected = HMACSHA1.HashData(Secret, firstInput);

        Assert.Equal(expected, PSha1.DeriveKeyMaterial(Secret, Seed, 20));
    }

    [Fact]
    public void LongerOutputChainsAThroughHmac()
    {
        var a1 = HMACSHA1.HashData(Secret, Seed);
        var a2 = HMACSHA1.HashData(Secret, a1);
        byte[] secondInput = [.. a2, .. Seed];
        var secondBlock = HMACSHA1.HashData(Secret, secondInput);

        var material = PSha1.DeriveKeyMaterial(Secret, Seed, 24);

        Assert.Equal(24, material.Length);
        Assert.Equal(secondBlock[..4], material[20..]);
    }

    [Fact]
    public void ShorterOutputIsAPrefixOfLonger()
    {
        var full = PSha1.DeriveKeyMaterial(Secret, Seed, 48);
        var prefix = PSha1.DeriveKeyMaterial(Secret, Seed, 24);

        Assert.Equal(full[..24], prefix);
    }
}
