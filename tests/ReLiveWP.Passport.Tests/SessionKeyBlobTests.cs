using System.Security.Cryptography;

namespace ReLiveWP.Passport.Tests;

public class SessionKeyBlobTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(24);
    private const string Nonce = "p9iyvjWExS8KCnK+9xdRLqy6OSo4hvOk";

    [Fact]
    public void ResponseForTheNonceVerifies()
    {
        var blob = SessionKeyBlob.CreateNonceResponse(Key, Nonce);

        Assert.Equal(28 + 8 + 20 + 40, blob.Length);
        Assert.True(SessionKeyBlob.VerifyNonceResponse(Key, Nonce, blob));
    }

    [Fact]
    public void WrongNonceOrKeyFails()
    {
        var blob = SessionKeyBlob.CreateNonceResponse(Key, Nonce);

        Assert.False(SessionKeyBlob.VerifyNonceResponse(Key, "someone else's nonce", blob));
        Assert.False(SessionKeyBlob.VerifyNonceResponse(RandomNumberGenerator.GetBytes(24), Nonce, blob));
    }

    [Fact]
    public void TruncatedOrGarbageFails()
    {
        var blob = SessionKeyBlob.CreateNonceResponse(Key, Nonce);

        Assert.False(SessionKeyBlob.VerifyNonceResponse(Key, Nonce, blob.AsSpan(0, 40)));
        Assert.False(SessionKeyBlob.VerifyNonceResponse(Key, Nonce, []));
        Assert.False(SessionKeyBlob.VerifyNonceResponse(Key, Nonce, RandomNumberGenerator.GetBytes(96)));
    }

    [Fact]
    public void IvLengthFromTheHeaderLocatesTheHash()
    {
        var blob = SessionKeyBlob.CreateNonceResponse(Key, Nonce);
        var withoutIv = new byte[blob.Length - 8];
        blob.AsSpan(0, 28).CopyTo(withoutIv);
        blob.AsSpan(36).CopyTo(withoutIv.AsSpan(28));
        BitConverter.GetBytes(0u).CopyTo(withoutIv, 16);

        Assert.True(SessionKeyBlob.VerifyNonceResponse(Key, Nonce, withoutIv));
    }
}
