using System.Security.Cryptography;
using System.Text;

namespace UnAccess.Data.Infrastructure;

internal static class ManagedIdentityPassword
{
    private const int Iterations = 100_000;
    private const int SaltSize = 128 / 8;
    private const int SubkeySize = 256 / 8;
    private const byte FormatMarker = 0x01;
    private const uint HmacSha512 = 2;

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var subkey = Derive(password, salt, HmacSha512, Iterations, SubkeySize);
        var output = new byte[13 + salt.Length + subkey.Length];
        output[0] = FormatMarker;
        WriteUInt32(output, 1, HmacSha512);
        WriteUInt32(output, 5, Iterations);
        WriteUInt32(output, 9, (uint)salt.Length);
        salt.CopyTo(output.AsSpan(13));
        subkey.CopyTo(output.AsSpan(13 + salt.Length));
        return Convert.ToBase64String(output);
    }

    public static bool Verify(string hashedPassword, string password)
    {
        if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(password))
            return false;

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(hashedPassword);
        }
        catch (FormatException)
        {
            return false;
        }

        if (decoded.Length < 13 || decoded[0] != FormatMarker)
            return false;

        var prf = ReadUInt32(decoded, 1);
        var iterations = ReadUInt32(decoded, 5);
        var saltLength = ReadUInt32(decoded, 9);
        if (saltLength < SaltSize || iterations < 1 || decoded.Length < 13 + saltLength + SaltSize)
            return false;

        var salt = decoded.AsSpan(13, (int)saltLength);
        var expected = decoded.AsSpan(13 + (int)saltLength);
        var actual = Derive(password, salt, prf, (int)iterations, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, ReadOnlySpan<byte> salt, uint prf, int iterations, int length)
    {
        using var hmac = CreateHmac(prf, Encoding.UTF8.GetBytes(password));
        var hashLength = hmac.HashSize / 8;
        var output = new byte[length];
        var block = new byte[salt.Length + 4];
        salt.CopyTo(block);
        var offset = 0;
        for (var blockIndex = 1; offset < length; blockIndex++)
        {
            block[^4] = (byte)(blockIndex >> 24);
            block[^3] = (byte)(blockIndex >> 16);
            block[^2] = (byte)(blockIndex >> 8);
            block[^1] = (byte)blockIndex;

            var u = hmac.ComputeHash(block);
            var mixed = (byte[])u.Clone();
            for (var iteration = 1; iteration < iterations; iteration++)
            {
                u = hmac.ComputeHash(u);
                for (var i = 0; i < mixed.Length; i++)
                    mixed[i] ^= u[i];
            }

            var take = Math.Min(hashLength, length - offset);
            Buffer.BlockCopy(mixed, 0, output, offset, take);
            offset += take;
        }

        return output;
    }

    private static HMAC CreateHmac(uint prf, byte[] password) => prf switch
    {
        0 => new HMACSHA1(password),
        1 => new HMACSHA256(password),
        HmacSha512 => new HMACSHA512(password),
        _ => throw new CryptographicException("Desteklenmeyen parola özeti.")
    };

    private static uint ReadUInt32(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24)
        | ((uint)buffer[offset + 1] << 16)
        | ((uint)buffer[offset + 2] << 8)
        | buffer[offset + 3];

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
