using System.Security.Cryptography;
using System.Text;

namespace BarRestPOS.Utils;

public static class PasswordHelper
{
    /// <summary>
    /// Hashea una contraseña usando SHA256 y la convierte a Base64
    /// </summary>
    public static string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }

    /// <summary>
    /// Verifica si una contraseña coincide con el hash almacenado
    /// </summary>
    public static bool VerifyPassword(string password, string hash)
    {
        var passwordHash = HashPassword(password);
        return passwordHash == hash;
    }
}

