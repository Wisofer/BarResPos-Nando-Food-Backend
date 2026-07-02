using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task RegistrarAccionAsync(string accion, string? mesaNumero, int? pedidoId, object? detalles, int? usuarioId = null)
    {
        try
        {
            int? finalUsuarioId = usuarioId;
            string? finalNombreUsuario = null;
            string? finalRolUsuario = null;

            // Intentar obtener usuario del HttpContext si no se especifica usuarioId
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;

            if (finalUsuarioId == null && user != null)
            {
                var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(idClaim, out var parsedId))
                {
                    finalUsuarioId = parsedId;
                }
            }

            // Si tenemos el ID del usuario, cargar su información
            if (finalUsuarioId.HasValue)
            {
                var dbUsuario = await _context.Usuarios.FindAsync(finalUsuarioId.Value);
                if (dbUsuario != null)
                {
                    finalNombreUsuario = dbUsuario.NombreCompleto ?? dbUsuario.NombreUsuario;
                    finalRolUsuario = dbUsuario.Rol;
                }
            }
            else if (user != null)
            {
                // Si no está en BD (raro) pero hay claims, usar la info de claims
                finalNombreUsuario = user.FindFirst("NombreCompleto")?.Value ?? user.FindFirst(ClaimTypes.Name)?.Value;
                finalRolUsuario = user.FindFirst("Rol")?.Value;
            }

            // Si sigue siendo null, es una acción automática del sistema
            finalNombreUsuario ??= "Sistema";
            finalRolUsuario ??= "Automatismo";

            // Serializar detalles a JSON
            string? detallesJson = null;
            if (detalles != null)
            {
                try
                {
                    detallesJson = JsonSerializer.Serialize(detalles, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    });
                }
                catch
                {
                    detallesJson = detalles.ToString();
                }
            }

            var log = new RegistroAuditoria
            {
                Fecha = DateTime.Now,
                UsuarioId = finalUsuarioId,
                NombreUsuario = finalNombreUsuario,
                RolUsuario = finalRolUsuario,
                Accion = accion,
                MesaNumero = mesaNumero,
                PedidoId = pedidoId,
                DetallesJson = detallesJson
            };

            _context.RegistrosAuditoria.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Fail-safe: No queremos que un fallo en auditoría rompa el flujo principal
            Console.WriteLine($"Error al registrar auditoria para la accion {accion}: {ex.Message}");
        }
    }

    public async Task<List<RegistroAuditoria>> ObtenerLogsAsync(
        DateTime? desde, 
        DateTime? hasta, 
        string? accion, 
        string? usuario, 
        string? mesa, 
        string? severidad, 
        int limit = 100)
    {
        var query = _context.RegistrosAuditoria
            .Include(r => r.Usuario)
            .AsQueryable();

        if (desde.HasValue)
        {
            query = query.Where(r => r.Fecha >= desde.Value);
        }

        if (hasta.HasValue)
        {
            query = query.Where(r => r.Fecha <= hasta.Value);
        }

        if (!string.IsNullOrWhiteSpace(accion))
        {
            var cleanAccion = accion.Trim().ToLower();
            query = query.Where(r => r.Accion.ToLower().Contains(cleanAccion));
        }

        if (!string.IsNullOrWhiteSpace(usuario))
        {
            var cleanUsuario = usuario.Trim().ToLower();
            query = query.Where(r => 
                (r.NombreUsuario != null && r.NombreUsuario.ToLower().Contains(cleanUsuario)) ||
                (r.Usuario != null && r.Usuario.NombreUsuario.ToLower().Contains(cleanUsuario))
            );
        }

        if (!string.IsNullOrWhiteSpace(mesa))
        {
            var cleanMesa = mesa.Trim().ToLower();
            query = query.Where(r => r.MesaNumero != null && r.MesaNumero.ToLower().Contains(cleanMesa));
        }

        if (!string.IsNullOrWhiteSpace(severidad))
        {
            var cleanSeveridad = severidad.Trim().ToLower();
            if (cleanSeveridad == "alta" || cleanSeveridad == "critica" || cleanSeveridad == "alerta")
            {
                var criticalActions = new[]
                {
                    "CancelacionConPin",
                    "CancelacionLineaConPin",
                    "AperturaManualCajon",
                    "DescuentoAplicado",
                    "DiferenciaCierre",
                    "AnulacionPedido"
                };
                query = query.Where(r => criticalActions.Contains(r.Accion));
            }
        }

        return await query
            .OrderByDescending(r => r.Fecha)
            .Take(limit)
            .ToListAsync();
    }
}
