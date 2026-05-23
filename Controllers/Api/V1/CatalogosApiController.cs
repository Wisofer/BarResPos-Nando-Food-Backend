using BarRestPOS.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/catalogos")]
public class CatalogosApiController : BaseApiController
{
    private readonly ApplicationDbContext _context;

    public CatalogosApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("categorias-producto")]
    public IActionResult CategoriasProducto()
    {
        var items = _context.CategoriasProducto
            .AsNoTracking()
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Nombre)
            .Select(c => new
            {
                c.Id,
                c.Nombre,
                c.Descripcion,
                c.IconoNombre,
                c.RequiereCocina,
                c.Activo
            })
            .ToList();
        return OkResponse(items);
    }

    [HttpGet("categorias-producto/{id:int}")]
    public IActionResult CategoriaProductoById(int id)
    {
        var item = _context.CategoriasProducto
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Nombre,
                c.Descripcion,
                c.IconoNombre,
                c.RequiereCocina,
                c.Activo
            })
            .FirstOrDefault();

        if (item == null) return FailResponse("Categoría no encontrada.", StatusCodes.Status404NotFound);
        return OkResponse(item);
    }

    [HttpPost("categorias-producto")]
    [Authorize(Policy = "Administrador")]
    public IActionResult CrearCategoriaProducto([FromBody] CategoriaProductoUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre)) return FailResponse("Nombre es requerido.");
        var nombre = request.Nombre.Trim();

        if (_context.CategoriasProducto.Any(c => c.Nombre.ToLower() == nombre.ToLower()))
            return FailResponse("Ya existe una categoría con ese nombre.");

        var maxOrden = _context.CategoriasProducto.Max(c => (int?)c.Orden) ?? 0;

        var item = new Models.Entities.CategoriaProducto
        {
            Nombre = nombre,
            Descripcion = request.Descripcion?.Trim(),
            IconoNombre = request.IconoNombre?.Trim(),
            Orden = maxOrden + 1,
            RequiereCocina = request.RequiereCocina,
            Activo = request.Activo
        };

        _context.CategoriasProducto.Add(item);
        _context.SaveChanges();
        return OkResponse(new { item.Id }, "Categoría creada");
    }

    [HttpPut("categorias-producto/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult ActualizarCategoriaProducto(int id, [FromBody] CategoriaProductoUpsertRequest request)
    {
        var item = _context.CategoriasProducto.FirstOrDefault(c => c.Id == id);
        if (item == null) return FailResponse("Categoría no encontrada.", StatusCodes.Status404NotFound);
        if (string.IsNullOrWhiteSpace(request.Nombre)) return FailResponse("Nombre es requerido.");

        var nombre = request.Nombre.Trim();
        if (_context.CategoriasProducto.Any(c => c.Id != id && c.Nombre.ToLower() == nombre.ToLower()))
            return FailResponse("Ya existe otra categoría con ese nombre.");

        item.Nombre = nombre;
        item.Descripcion = request.Descripcion?.Trim();
        item.IconoNombre = request.IconoNombre?.Trim();
        item.RequiereCocina = request.RequiereCocina;
        item.Activo = request.Activo;

        _context.SaveChanges();
        return OkResponse(new { item.Id }, "Categoría actualizada");
    }

    [HttpDelete("categorias-producto/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult EliminarCategoriaProducto(int id)
    {
        var item = _context.CategoriasProducto.FirstOrDefault(c => c.Id == id);
        if (item == null) return FailResponse("Categoría no encontrada.", StatusCodes.Status404NotFound);

        var enUso = _context.Servicios.Any(s => s.CategoriaProductoId == id && s.Activo);
        if (enUso) return FailResponse("No se puede eliminar una categoría en uso.");

        item.Activo = false;
        _context.SaveChanges();
        return OkResponse(new { item.Id }, "Categoría desactivada");
    }

    [HttpGet("ubicaciones")]
    public IActionResult Ubicaciones()
    {
        var items = _context.Ubicaciones
            .AsNoTracking()
            .OrderBy(u => u.Nombre)
            .Select(u => new
            {
                u.Id,
                u.Nombre,
                u.Descripcion,
                u.Activo
            })
            .ToList();
        return OkResponse(items);
    }

    [HttpGet("ubicaciones/{id:int}")]
    public IActionResult UbicacionById(int id)
    {
        var item = _context.Ubicaciones
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.Nombre,
                u.Descripcion,
                u.Activo
            })
            .FirstOrDefault();

        if (item == null) return FailResponse("Ubicación no encontrada.", StatusCodes.Status404NotFound);
        return OkResponse(item);
    }

    [HttpPost("ubicaciones")]
    [Authorize(Policy = "Administrador")]
    public IActionResult CrearUbicacion([FromBody] UbicacionUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre)) return FailResponse("Nombre es requerido.");
        var nombre = request.Nombre.Trim();

        if (_context.Ubicaciones.Any(u => u.Nombre.ToLower() == nombre.ToLower()))
            return FailResponse("Ya existe una ubicación con ese nombre.");

        var item = new Models.Entities.Ubicacion
        {
            Nombre = nombre,
            Descripcion = request.Descripcion?.Trim(),
            Activo = request.Activo
        };

        _context.Ubicaciones.Add(item);
        _context.SaveChanges();
        return OkResponse(new { item.Id }, "Ubicación creada");
    }

    [HttpPut("ubicaciones/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult ActualizarUbicacion(int id, [FromBody] UbicacionUpsertRequest request)
    {
        var item = _context.Ubicaciones.FirstOrDefault(u => u.Id == id);
        if (item == null) return FailResponse("Ubicación no encontrada.", StatusCodes.Status404NotFound);
        if (string.IsNullOrWhiteSpace(request.Nombre)) return FailResponse("Nombre es requerido.");

        var nombre = request.Nombre.Trim();
        if (_context.Ubicaciones.Any(u => u.Id != id && u.Nombre.ToLower() == nombre.ToLower()))
            return FailResponse("Ya existe otra ubicación con ese nombre.");

        item.Nombre = nombre;
        item.Descripcion = request.Descripcion?.Trim();
        item.Activo = request.Activo;

        _context.SaveChanges();
        return OkResponse(new { item.Id }, "Ubicación actualizada");
    }

    [HttpDelete("ubicaciones/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult EliminarUbicacion(int id)
    {
        var item = _context.Ubicaciones.FirstOrDefault(u => u.Id == id);
        if (item == null) return FailResponse("Ubicación no encontrada.", StatusCodes.Status404NotFound);

        var enUso = _context.Mesas.Any(m => m.UbicacionId == id && m.Activo);
        if (enUso) return FailResponse("No se puede eliminar una ubicación en uso por mesas activas.");

        item.Activo = false;
        _context.SaveChanges();
        return OkResponse(new { item.Id }, "Ubicación desactivada");
    }

    [HttpGet("proveedores")]
    public IActionResult Proveedores()
    {
        var items = _context.Proveedores
            .AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Telefono,
                p.Email,
                p.Direccion,
                p.Contacto,
                p.Observaciones,
                p.Activo
            })
            .ToList();
        return OkResponse(items);
    }

    [HttpGet("proveedores/{id:int}")]
    public IActionResult ProveedorById(int id)
    {
        var item = _context.Proveedores
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Telefono,
                p.Email,
                p.Direccion,
                p.Contacto,
                p.Observaciones,
                p.Activo
            })
            .FirstOrDefault();

        if (item == null) return FailResponse("Proveedor no encontrado.", StatusCodes.Status404NotFound);
        return OkResponse(item);
    }

    [HttpPost("proveedores")]
    [Authorize(Policy = "Administrador")]
    public IActionResult CrearProveedor([FromBody] ProveedorUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre)) return FailResponse("Nombre es requerido.");
        var nombre = request.Nombre.Trim();

        if (_context.Proveedores.Any(p => p.Nombre.ToLower() == nombre.ToLower()))
            return FailResponse("Ya existe un proveedor con ese nombre.");

        var item = new Models.Entities.Proveedor
        {
            Nombre = nombre,
            Telefono = request.Telefono?.Trim(),
            Email = request.Email?.Trim(),
            Direccion = request.Direccion?.Trim(),
            Contacto = request.Contacto?.Trim(),
            Observaciones = request.Observaciones?.Trim(),
            Activo = request.Activo
        };

        _context.Proveedores.Add(item);
        _context.SaveChanges();
        return OkResponse(new { item.Id }, "Proveedor creado");
    }

    [HttpPut("proveedores/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult ActualizarProveedor(int id, [FromBody] ProveedorUpsertRequest request)
    {
        var item = _context.Proveedores.FirstOrDefault(p => p.Id == id);
        if (item == null) return FailResponse("Proveedor no encontrado.", StatusCodes.Status404NotFound);
        if (string.IsNullOrWhiteSpace(request.Nombre)) return FailResponse("Nombre es requerido.");

        var nombre = request.Nombre.Trim();
        if (_context.Proveedores.Any(p => p.Id != id && p.Nombre.ToLower() == nombre.ToLower()))
            return FailResponse("Ya existe otro proveedor con ese nombre.");

        item.Nombre = nombre;
        item.Telefono = request.Telefono?.Trim();
        item.Email = request.Email?.Trim();
        item.Direccion = request.Direccion?.Trim();
        item.Contacto = request.Contacto?.Trim();
        item.Observaciones = request.Observaciones?.Trim();
        item.Activo = request.Activo;

        _context.SaveChanges();
        return OkResponse(new { item.Id }, "Proveedor actualizado");
    }

    [HttpDelete("proveedores/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult EliminarProveedor(int id)
    {
        var item = _context.Proveedores.FirstOrDefault(p => p.Id == id);
        if (item == null) return FailResponse("Proveedor no encontrado.", StatusCodes.Status404NotFound);

        item.Activo = false;
        _context.SaveChanges();
        return OkResponse(new { item.Id }, "Proveedor desactivado");
    }
}

public class CategoriaProductoUpsertRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? IconoNombre { get; set; }
    public bool RequiereCocina { get; set; } = true;
    public bool Activo { get; set; } = true;
}

public class ProveedorUpsertRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Contacto { get; set; }
    public string? Observaciones { get; set; }
    public bool Activo { get; set; } = true;
}

public class UbicacionUpsertRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}
