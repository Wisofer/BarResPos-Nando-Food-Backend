using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BarRestPOS.Services.IServices;
using Microsoft.IdentityModel.Tokens;

namespace BarRestPOS.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, string JwtId, DateTime ExpiresAtUtc) GenerateAccessToken(IEnumerable<Claim> claims)
    {
        var secret = _configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey no configurado.");
        var issuer = _configuration["JwtSettings:Issuer"] ?? "BarRestPOS";
        var audience = _configuration["JwtSettings:Audience"] ?? "BarRestPOSClients";
        var expirationMinutes = int.TryParse(_configuration["JwtSettings:ExpirationInMinutes"], out var mins) ? mins : 60;

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(expirationMinutes);
        var jwtId = Guid.NewGuid().ToString("N");

        var tokenClaims = claims.ToList();
        tokenClaims.Add(new Claim(JwtRegisteredClaimNames.Jti, jwtId));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: tokenClaims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), jwtId, expires);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
