using System.Security.Cryptography;

namespace Universe2D.AuthServer;

/// <summary>
/// Hachage de mot de passe avec PBKDF2 (HMAC-SHA256) : un sel aleatoire
/// different par compte, un grand nombre d'iterations pour ralentir les
/// attaques par force brute, et jamais le mot de passe en clair stocke
/// nulle part (ni en base, ni dans un log).
/// </summary>
public static class PasswordHasher
{
    private const int SaltSizeBytes = 16; // 128 bits
    private const int HashSizeBytes = 32; // 256 bits
    public const int DefaultIterations = 100_000;

    public static (byte[] hash, byte[] salt, int iterations) Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] hash = Derive(password, salt, DefaultIterations);
        return (hash, salt, DefaultIterations);
    }

    public static bool Verify(string password, byte[] expectedHash, byte[] salt, int iterations)
    {
        byte[] actualHash = Derive(password, salt, iterations);

        // Comparaison en temps constant : sinon le temps de reponse peut
        // laisser deviner, octet par octet, a quel point un hash essaye se
        // rapproche du bon (attaque par canal auxiliaire).
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSizeBytes);
    }
}
