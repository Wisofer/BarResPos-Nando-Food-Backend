using System.Security.Claims;

namespace BarRestPOS.Utils;

/// <summary>
/// Helper centralizado para operaciones de seguridad y autenticación.
/// Usa Claims en lugar de Session para mayor seguridad.
/// </summary>
public static class SecurityHelper
{
    /// <summary>
    /// Obtiene el ID del usuario actual desde Claims
    /// </summary>
    public static int? GetUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }

    /// <summary>
    /// Obtiene el nombre de usuario actual desde Claims
    /// </summary>
    public static string GetUserName(ClaimsPrincipal user)
    {
        return user.Identity?.Name ?? "";
    }

    /// <summary>
    /// Obtiene el rol del usuario actual desde Claims
    /// </summary>
    public static string GetUserRole(ClaimsPrincipal user)
    {
        return user.FindFirst("Rol")?.Value ?? "";
    }

    /// <summary>
    /// Obtiene el nombre completo del usuario desde Claims
    /// </summary>
    public static string GetUserFullName(ClaimsPrincipal user)
    {
        return user.FindFirst("NombreCompleto")?.Value ?? GetUserName(user);
    }

    /// <summary>
    /// Verifica si el usuario actual es Administrador
    /// </summary>
    public static bool IsAdministrator(ClaimsPrincipal user)
    {
        return GetUserRole(user).Equals("Administrador", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifica si el usuario actual tiene un rol específico
    /// </summary>
    public static bool HasRole(ClaimsPrincipal user, string role)
    {
        return GetUserRole(user).Equals(role, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifica si el usuario actual tiene alguno de los roles especificados
    /// </summary>
    public static bool HasAnyRole(ClaimsPrincipal user, params string[] roles)
    {
        var userRole = GetUserRole(user);
        return roles.Any(r => userRole.Equals(r, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Obtiene la URL de redirección según el rol del usuario
    /// </summary>
    public static string GetRedirectUrlByRole(ClaimsPrincipal user)
    {
        var rol = GetUserRole(user);
        return rol switch
        {
            SD.RolAdministrador => "/",
            SD.RolMesero => "/mesas",        // Mesero va a Mesas
            SD.RolCocinero => "/cocina",     // Cocinero va a Cocina
            SD.RolCajero => "/caja",         // Cajero va a Caja
            SD.RolNormal => "/facturas",     // Rol antiguo (compatibilidad)
            SD.RolCaja => "/caja",           // Rol antiguo (compatibilidad)
            _ => "/mesas"                    // Por defecto, ir a Mesas (más seguro que /login)
        };
    }
}

