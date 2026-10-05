using System.Security.Cryptography;

namespace ReLiveWP.Passport;

public static class PSha1
{
    public static byte[] DeriveKeyMaterial(byte[] secret, byte[] seed, int length)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        var output = new byte[length];
        using var hmac = new HMACSHA1(secret);

        var a = hmac.ComputeHash(seed);
        var filled = 0;
        while (filled < length)
        {
            var block = hmac.ComputeHash([.. a, .. seed]);
            var take = Math.Min(block.Length, length - filled);
            block.AsSpan(0, take).CopyTo(output.AsSpan(filled));
            filled += take;
            a = hmac.ComputeHash(a);
        }

        return output;
    }
}
