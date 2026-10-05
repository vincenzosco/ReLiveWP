using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ReLiveWP.Passport;

public static class SessionKeyBlob
{
    private const int HeaderLength = 28;
    private const uint CryptMode = 1;
    private const uint Calg3Des = 0x6603;
    private const uint CalgSha1 = 0x8004;
    private const int HashLength = 20;
    private const int IvLength = 8;

    private static ReadOnlySpan<byte> HashKeySeed => "WS-SecureConversationSESSION KEY HASH"u8;
    private static ReadOnlySpan<byte> EncryptionKeySeed => "WS-SecureConversationSESSION KEY ENCRYPTION"u8;

    public static bool VerifyNonceResponse(byte[] sessionKey, string nonce, ReadOnlySpan<byte> blob)
    {
        if (blob.Length < HeaderLength || BinaryPrimitives.ReadUInt32LittleEndian(blob) != HeaderLength)
            return false;

        var ivLength = BinaryPrimitives.ReadUInt32LittleEndian(blob[16..]);
        var hashLength = BinaryPrimitives.ReadUInt32LittleEndian(blob[20..]);
        if (hashLength != HashLength || ivLength > blob.Length - HeaderLength - HashLength)
            return false;

        var hash = blob.Slice(HeaderLength + (int)ivLength, HashLength);
        var expected = HMACSHA1.HashData(DeriveHashKey(sessionKey), Encoding.UTF8.GetBytes(nonce));
        return CryptographicOperations.FixedTimeEquals(hash, expected);
    }

    public static byte[] CreateNonceResponse(byte[] sessionKey, string nonce)
    {
        var plaintext = Encoding.UTF8.GetBytes(nonce);
        var hash = HMACSHA1.HashData(DeriveHashKey(sessionKey), plaintext);

        using var des = TripleDES.Create();
        des.Mode = CipherMode.CBC;
        des.Padding = PaddingMode.PKCS7;
        des.Key = PSha1.DeriveKeyMaterial(sessionKey, EncryptionKeySeed.ToArray(), sessionKey.Length);
        des.GenerateIV();
        var cipher = des.EncryptCbc(plaintext, des.IV);

        var blob = new byte[HeaderLength + IvLength + HashLength + cipher.Length];
        var header = blob.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(header, HeaderLength);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], CryptMode);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], Calg3Des);
        BinaryPrimitives.WriteUInt32LittleEndian(header[12..], CalgSha1);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], IvLength);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], HashLength);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)cipher.Length);
        des.IV.CopyTo(blob, HeaderLength);
        hash.CopyTo(blob, HeaderLength + IvLength);
        cipher.CopyTo(blob, HeaderLength + IvLength + HashLength);
        return blob;
    }

    private static byte[] DeriveHashKey(byte[] sessionKey) =>
        PSha1.DeriveKeyMaterial(sessionKey, HashKeySeed.ToArray(), sessionKey.Length);
}
