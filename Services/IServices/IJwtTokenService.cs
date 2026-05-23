using System.Security.Claims;

namespace BarRestPOS.Services.IServices;

public interface IJwtTokenService
{
    (string Token, string JwtId, DateTime ExpiresAtUtc) GenerateAccessToken(IEnumerable<Claim> claims);
    string GenerateRefreshToken();
    string HashToken(string rawToken);
}
