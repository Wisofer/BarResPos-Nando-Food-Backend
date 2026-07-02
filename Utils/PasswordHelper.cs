using System.Security.Cryptography;
using System.Text;

namespace BarRestPOS.Utils;

public static class PasswordHelper
{
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public static bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return false;

        // BCrypt hashes start with "$2"
        if (hash.StartsWith("$2"))
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }

        // Legacy SHA256 fallback (migrate to BCrypt on successful verification)
        var legacyHash = LegacyHash(password);
        if (legacyHash == hash)
        {
            return true;
        }

        return false;
    }

    private static string LegacyHash(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}
