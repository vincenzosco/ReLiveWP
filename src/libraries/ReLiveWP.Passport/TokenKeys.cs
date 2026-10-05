using System.Security.Cryptography;
using System.Text;

namespace ReLiveWP.Passport;

public enum TokenKeyPurpose
{
    DeviceSession,
    ServiceProof,
}

public static class TokenKeys
{
    public const int KeyLength = 24;

    public static byte[] DeriveTokenKey(byte[] root, TokenKeyPurpose purpose, string tokenId)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrEmpty(tokenId);

        var info = Encoding.UTF8.GetBytes($"relivewp token key\n{purpose}\n{tokenId}");
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, root, KeyLength, salt: null, info: info);
    }
}
