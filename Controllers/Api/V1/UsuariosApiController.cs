using BarRestPOS.Data;
using BarRestPOS.Models.Api;
using BarRestPOS.Models.Entities;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize(Policy = "Administrador")]
[Route("api/v1/usuarios")]
public class UsuariosApiController : BaseApiController
{
    private readonly ApplicationDbContext _context;

    public UsuariosApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult GetAll([FromQuery] string? search, [FromQuery] string? rol, [FromQuery] bool? activo, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _context.Usuarios.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(u => u.NombreUsuario.ToLower().Contains(q) || u.NombreCompleto.ToLower().Contains(q));
        }
        if (!string.IsNullOrWhiteSpace(rol)) query = query.Where(u => u.Rol == rol);
        if (activo.HasValue) query = query.Where(u => u.Activo == activo.Value);

        var total = query.Count();
        var items = query.OrderBy(u => u.NombreUsuario)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.NombreUsuario,
                u.NombreCompleto,
                u.Rol,
                u.Activo
            })
            .ToList();

        return OkResponse(new PagedResult<object>
        {
            Items = items.Cast<object>().ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    [HttpPost]
    public IActionResult Create([FromBody] UsuarioUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreUsuario) || string.IsNullOrWhiteSpace(request.Contrasena) || string.IsNullOrWhiteSpace(request.NombreCompleto))
            return FailResponse("NombreUsuario, Contrasena y NombreCompleto son requeridos.");

        if (_context.Usuarios.Any(u => u.NombreUsuario == request.NombreUsuario))
            return FailResponse("Ya existe un usuario con ese nombre.");

        var usuario = new Usuario
        {
            NombreUsuario = request.NombreUsuario.Trim(),
            NombreCompleto = request.NombreCompleto.Trim(),
            Rol = string.IsNullOrWhiteSpace(request.Rol) ? SD.RolMesero : request.Rol.Trim(),
            Contrasena = PasswordHelper.HashPassword(request.Contrasena),
            Activo = request.Activo
        };

        _context.Usuarios.Add(usuario);
        _context.SaveChanges();
        return OkResponse(new { usuario.Id }, "Usuario creado");
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] UsuarioUpsertRequest request)
    {
        var usuario = _context.Usuarios.FirstOrDefault(u => u.Id == id);
        if (usuario == null) return FailResponse("Usuario no encontrado.", StatusCodes.Status404NotFound);

        if (!string.IsNullOrWhiteSpace(request.NombreUsuario) &&
            _context.Usuarios.Any(u => u.Id != id && u.NombreUsuario == request.NombreUsuario))
        {
            return FailResponse("Ya existe un usuario con ese nombre.");
        }

        if (!string.IsNullOrWhiteSpace(request.NombreUsuario)) usuario.NombreUsuario = request.NombreUsuario.Trim();
        if (!string.IsNullOrWhiteSpace(request.NombreCompleto)) usuario.NombreCompleto = request.NombreCompleto.Trim();
        if (!string.IsNullOrWhiteSpace(request.Rol)) usuario.Rol = request.Rol.Trim();
        if (!string.IsNullOrWhiteSpace(request.Contrasena)) usuario.Contrasena = PasswordHelper.HashPassword(request.Contrasena);
        usuario.Activo = request.Activo;

        _context.SaveChanges();
        return OkResponse(new { usuario.Id }, "Usuario actualizado");
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var usuario = _context.Usuarios.FirstOrDefault(u => u.Id == id);
        if (usuario == null) return FailResponse("Usuario no encontrado.", StatusCodes.Status404NotFound);
        if (usuario.NombreUsuario.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return FailResponse("No se puede eliminar el usuario admin.");

        _context.Usuarios.Remove(usuario);
        _context.SaveChanges();
        return OkResponse(new { id }, "Usuario eliminado");
    }
}

public class UsuarioUpsertRequest
{
    public string NombreUsuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Rol { get; set; } = SD.RolMesero;
    public string? Contrasena { get; set; }
    public bool Activo { get; set; } = true;
}
