using System.Security.Cryptography;

namespace MomsLove.Core;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static PasswordSettings Create(string password)
    {
        ValidatePassword(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return new PasswordSettings
        {
            Salt = salt,
            Hash = Hash(password, salt, 120_000),
            Iterations = 120_000
        };
    }

    public static bool Verify(PasswordSettings settings, string password)
    {
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var candidate = Hash(password, settings.Salt, settings.Iterations);
        return CryptographicOperations.FixedTimeEquals(candidate, settings.Hash);
    }

    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("密码不能为空。", nameof(password));
        }
    }

    private static byte[] Hash(string password, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize);
    }
}
