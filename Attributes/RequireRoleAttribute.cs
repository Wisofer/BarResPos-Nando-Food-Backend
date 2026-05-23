using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace BarRestPOS.Attributes;

/// <summary>
/// Atributo personalizado para requerir un rol específico.
/// Centraliza la verificación de permisos basada en roles.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _allowedRoles;

    public RequireRoleAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles ?? throw new ArgumentNullException(nameof(allowedRoles));
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Si el usuario no está autenticado, el middleware [Authorize] ya lo maneja
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            return; // El middleware de autenticación se encargará de redirigir
        }

        // Obtener el rol del usuario desde Claims
        var userRole = context.HttpContext.User.FindFirst("Rol")?.Value;

        // Si no tiene rol o el rol no está permitido
        if (string.IsNullOrEmpty(userRole) || !_allowedRoles.Contains(userRole, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = new ForbidResult();
        }
    }
}

