using System.Security.Claims;
using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Controllers.Api.V1;

[Route("api/v1/auth")]
public class AuthApiController : BaseApiController
{
    private readonly IAuthService _authService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthApiController> _logger;
    private const int RefreshTokenDays = 15;

    public AuthApiController(
        IAuthService authService,
        IJwtTokenService jwtTokenService,
        ApplicationDbContext context,
        ILogger<AuthApiController> logger)
    {
        _authService = authService;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreUsuario) || string.IsNullOrWhiteSpace(request.Contrasena))
        {
            return FailResponse("Usuario y contraseña son requeridos.");
        }

        var usuario = _authService.ValidarUsuario(request.NombreUsuario, request.Contrasena);
        if (usuario == null)
        {
            return FailResponse("Credenciales inválidas.", StatusCodes.Status401Unauthorized);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, usuario.NombreUsuario),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new("Rol", usuario.Rol),
            new("NombreCompleto", usuario.NombreCompleto)
        };

        var jwtToken = _jwtTokenService.GenerateAccessToken(claims);
        var refreshTokenRaw = _jwtTokenService.GenerateRefreshToken();
        var refreshTokenHash = _jwtTokenService.HashToken(refreshTokenRaw);

        _context.RefreshTokens.Add(new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = refreshTokenHash,
            JwtId = jwtToken.JwtId,
            CreadoEnUtc = DateTime.UtcNow,
            ExpiraEnUtc = DateTime.UtcNow.AddDays(RefreshTokenDays)
        });
        _context.SaveChanges();

        return OkResponse(new
        {
            usuario.Id,
            usuario.NombreUsuario,
            usuario.NombreCompleto,
            usuario.Rol,
            RedirectUrl = "/",
            AccessToken = jwtToken.Token,
            ExpiresAt = jwtToken.ExpiresAtUtc,
            RefreshToken = refreshTokenRaw,
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays)
        }, "Login exitoso");
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public IActionResult Refresh([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return FailResponse("Refresh token requerido.");

        var hashed = _jwtTokenService.HashToken(request.RefreshToken);
        var tokenEntity = _context.RefreshTokens
            .Include(t => t.Usuario)
            .FirstOrDefault(t => t.TokenHash == hashed);

        if (tokenEntity == null || tokenEntity.Usuario == null || !tokenEntity.Usuario.Activo)
            return FailResponse("Refresh token inválido.", StatusCodes.Status401Unauthorized);

        if (!tokenEntity.Activo)
            return FailResponse("Refresh token expirado o revocado.", StatusCodes.Status401Unauthorized);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, tokenEntity.Usuario.NombreUsuario),
            new(ClaimTypes.NameIdentifier, tokenEntity.Usuario.Id.ToString()),
            new("Rol", tokenEntity.Usuario.Rol),
            new("NombreCompleto", tokenEntity.Usuario.NombreCompleto)
        };

        var newJwt = _jwtTokenService.GenerateAccessToken(claims);
        var newRefreshRaw = _jwtTokenService.GenerateRefreshToken();
        var newRefreshHash = _jwtTokenService.HashToken(newRefreshRaw);

        tokenEntity.RevocadoEnUtc = DateTime.UtcNow;
        tokenEntity.ReemplazadoPorTokenHash = newRefreshHash;
        tokenEntity.MotivoRevocacion = "Rotación por refresh";

        _context.RefreshTokens.Add(new RefreshToken
        {
            UsuarioId = tokenEntity.UsuarioId,
            TokenHash = newRefreshHash,
            JwtId = newJwt.JwtId,
            CreadoEnUtc = DateTime.UtcNow,
            ExpiraEnUtc = DateTime.UtcNow.AddDays(RefreshTokenDays)
        });
        _context.SaveChanges();

        return OkResponse(new
        {
            AccessToken = newJwt.Token,
            ExpiresAt = newJwt.ExpiresAtUtc,
            RefreshToken = newRefreshRaw,
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays)
        }, "Token renovado");
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout([FromBody] RevokeTokenRequest? request = null)
    {
        var userId = SecurityHelper.GetUserId(User);
        if (userId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
            {
                var hash = _jwtTokenService.HashToken(request.RefreshToken);
                var token = _context.RefreshTokens.FirstOrDefault(t => t.UsuarioId == userId && t.TokenHash == hash);
                if (token != null && token.Activo)
                {
                    token.RevocadoEnUtc = DateTime.UtcNow;
                    token.MotivoRevocacion = "Logout";
                }
            }
            else
            {
                var activos = _context.RefreshTokens.Where(t => t.UsuarioId == userId && t.RevocadoEnUtc == null && t.ExpiraEnUtc > DateTime.UtcNow).ToList();
                foreach (var t in activos)
                {
                    t.RevocadoEnUtc = DateTime.UtcNow;
                    t.MotivoRevocacion = "Logout global";
                }
            }
            _context.SaveChanges();
        }

        return OkResponse(new { }, "Sesión cerrada y tokens revocados");
    }

    [HttpPost("revoke")]
    [Authorize]
    public IActionResult Revoke([FromBody] RevokeTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return FailResponse("Refresh token requerido.");

        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) return FailResponse("Usuario no autenticado.", StatusCodes.Status401Unauthorized);

        var hashed = _jwtTokenService.HashToken(request.RefreshToken);
        var token = _context.RefreshTokens.FirstOrDefault(t => t.UsuarioId == userId && t.TokenHash == hashed);
        if (token == null) return FailResponse("Refresh token no encontrado.", StatusCodes.Status404NotFound);
        if (!token.Activo) return FailResponse("Refresh token ya no está activo.");

        token.RevocadoEnUtc = DateTime.UtcNow;
        token.MotivoRevocacion = "Revocado manualmente";
        _context.SaveChanges();

        return OkResponse(new { }, "Refresh token revocado");
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var payload = new
        {
            Id = SecurityHelper.GetUserId(User),
            UserName = SecurityHelper.GetUserName(User),
            FullName = SecurityHelper.GetUserFullName(User),
            Role = SecurityHelper.GetUserRole(User)
        };
        return OkResponse(payload);
    }

}

public class LoginRequest
{
    public string NombreUsuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RevokeTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
