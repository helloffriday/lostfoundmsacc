using System.Security.Cryptography;

namespace LostAndFound.Auth;

/// <summary>Salted PBKDF2-SHA256 password hashing.</summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    public const int DefaultIterations = 210_000;

    public static (string Hash, string Salt, int Iterations) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, HashAlgorithmName.SHA256, KeySize);
        return (Convert.ToBase64String(key), Convert.ToBase64String(salt), DefaultIterations);
    }

    public static bool Verify(string password, string hash, string salt, int iterations)
    {
        if (hash.Length == 0 || salt.Length == 0 || iterations <= 0) return false;
        var key = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(salt), iterations, HashAlgorithmName.SHA256, KeySize);
        return CryptographicOperations.FixedTimeEquals(key, Convert.FromBase64String(hash));
    }
}
