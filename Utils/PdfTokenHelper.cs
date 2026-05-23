using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace BarRestPOS.Utils;

/// <summary>
/// Helper para generar y validar tokens seguros para descarga pública de PDFs
/// </summary>
public static class PdfTokenHelper
{
    private static string GetSecretKey(IConfiguration configuration)
    {
        return configuration["JwtSettings:SecretKey"] 
            ?? "EstaEsUnaClaveSecretaMuyLargaParaJWT2024EMSINETBillingSystem";
    }

    /// <summary>
    /// Genera un token seguro para una factura específica
    /// </summary>
    public static string GenerarToken(int facturaId, IConfiguration configuration)
    {
        var secretKey = GetSecretKey(configuration);
        var data = $"{facturaId}_{secretKey}";
        
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").Replace("=", "").Substring(0, 32);
        }
    }

    /// <summary>
    /// Valida si un token es válido para una factura específica
    /// </summary>
    public static bool ValidarToken(int facturaId, string token, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var tokenGenerado = GenerarToken(facturaId, configuration);
        return tokenGenerado.Equals(token, StringComparison.Ordinal);
    }

    /// <summary>
    /// Genera token temporal con expiración para enlaces públicos de PDF.
    /// Formato: "expUnix.signatureBase64Url"
    /// </summary>
    public static string GenerarTokenTemporal(int facturaId, IConfiguration configuration, TimeSpan? vigencia = null)
    {
        var secretKey = GetSecretKey(configuration);
        var expUnix = DateTimeOffset.UtcNow.Add(vigencia ?? TimeSpan.FromHours(12)).ToUnixTimeSeconds();
        var payload = $"{facturaId}.{expUnix}";
        var signature = FirmarPayload(payload, secretKey);
        return $"{expUnix}.{signature}";
    }

    /// <summary>
    /// Valida token temporal firmado y vencimiento.
    /// </summary>
    public static bool ValidarTokenTemporal(int facturaId, string? token, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var parts = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        if (!long.TryParse(parts[0], out var expUnix))
            return false;

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expUnix)
            return false;

        var payload = $"{facturaId}.{expUnix}";
        var expected = FirmarPayload(payload, GetSecretKey(configuration));
        return EqualsFixedTime(expected, parts[1]);
    }

    private static string FirmarPayload(string payload, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static bool EqualsFixedTime(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return aBytes.Length == bBytes.Length && CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}

