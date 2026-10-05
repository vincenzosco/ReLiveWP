using System.Security.Cryptography;

namespace ReLiveWP.Passport.Tests;

public class TokenKeysTests
{
    private static readonly byte[] Root = Convert.FromBase64String("dW5pdCB0ZXN0IHN0cyBrZXkgMjRieXRl");

    [Fact]
    public void SameInputsGiveTheSameKey()
    {
        var first = TokenKeys.DeriveTokenKey(Root, TokenKeyPurpose.ServiceProof, "jti-1");
        var second = TokenKeys.DeriveTokenKey(Root, TokenKeyPurpose.ServiceProof, "jti-1");

        Assert.Equal(TokenKeys.KeyLength, first.Length);
        Assert.Equal(first, second);
    }

    [Fact]
    public void PurposeTokenAndRootEachChangeTheKey()
    {
        var baseline = TokenKeys.DeriveTokenKey(Root, TokenKeyPurpose.ServiceProof, "jti-1");
        var otherRoot = RandomNumberGenerator.GetBytes(24);

        Assert.NotEqual(baseline, TokenKeys.DeriveTokenKey(Root, TokenKeyPurpose.DeviceSession, "jti-1"));
        Assert.NotEqual(baseline, TokenKeys.DeriveTokenKey(Root, TokenKeyPurpose.ServiceProof, "jti-2"));
        Assert.NotEqual(baseline, TokenKeys.DeriveTokenKey(otherRoot, TokenKeyPurpose.ServiceProof, "jti-1"));
    }
}
