using System.Security.Cryptography;
using dependancies.SharedClasses;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace dependancies.Security;

public interface IHash
{
    public string Hash(string text, string salt = "");
    public bool CompareTextAndHash(string? text, string? hash);
}

/// <summary>
/// Hash PBKDF2 (HMACSHA256, 100 000 itérations). Format stocké : "{hash}||{salt}" en base64.
/// </summary>
public class HashPBKDF2 : IHash, ISingleton
{
    private const int ITERATIONS = 100_000;

    public string Hash(string text, string salt = "")
    {
        byte[] byteSalt = string.IsNullOrEmpty(salt)
            ? RandomNumberGenerator.GetBytes(128 / 8)
            : Convert.FromBase64String(salt);

        var hashed = KeyDerivation.Pbkdf2(
            password: text,
            salt: byteSalt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: ITERATIONS,
            numBytesRequested: 256 / 8);

        return Convert.ToBase64String(hashed) + "||" + Convert.ToBase64String(byteSalt);
    }

    public bool CompareTextAndHash(string? text, string? hash)
    {
        if (text is null || hash is null)
            return false;

        var parts = hash.Split("||");
        if (parts.Length != 2)
            return false;

        var expected = Convert.FromBase64String(parts[0]);
        var actual = Convert.FromBase64String(Hash(text, parts[1]).Split("||")[0]);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
