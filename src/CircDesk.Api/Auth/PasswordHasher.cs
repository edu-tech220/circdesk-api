using System.Security.Cryptography;

namespace CircDesk.Api.Auth;

/// <summary>PBKDF2 (HMAC-SHA256) password hashing, salted per member.</summary>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public (byte[] Hash, byte[] Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt);
        return (hash, salt);
    }

    public bool Verify(string password, byte[] hash, byte[] salt)
    {
        var computed = Derive(password, salt);
        return CryptographicOperations.FixedTimeEquals(computed, hash);
    }

    private static byte[] Derive(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
}
