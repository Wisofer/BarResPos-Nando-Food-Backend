using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Controllers.Api.V1;

/// <summary>CRUD de grupos y opciones configurables por producto (admin).</summary>
[Authorize(Policy = "Administrador")]
[Route("api/v1/productos/{productoId:int}/opciones")]
public class ProductoOpcionesApiController : BaseApiController
{
    private readonly ApplicationDbContext _context;

    public ProductoOpcionesApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("grupos")]
    public IActionResult ListarGrupos(int productoId)
    {
        if (!ExisteProducto(productoId)) return FailResponse("Producto no encontrado.", StatusCodes.Status404NotFound);

        var grupos = _context.ProductoOpcionGrupos
            .AsNoTracking()
            .Include(g => g.Opciones)
            .Where(g => g.ServicioId == productoId)
            .OrderBy(g => g.Orden)
            .Select(g => new
            {
                g.Id,
                g.Nombre,
                g.Orden,
                g.Obligatorio,
                g.MinSeleccion,
                g.MaxSeleccion,
                g.ReemplazaPrecioBase,
                g.Activo,
                opciones = g.Opciones.OrderBy(o => o.Orden).Select(o => new
                {
                    o.Id,
                    o.Nombre,
                    o.Orden,
                    o.PrecioAdicional,
                    o.Activo
                })
            })
            .ToList();

        return OkResponse(grupos);
    }

    [HttpPost("grupos")]
    public IActionResult CrearGrupo(int productoId, [FromBody] ProductoOpcionGrupoUpsertRequest body)
    {
        if (!ExisteProducto(productoId)) return FailResponse("Producto no encontrado.", StatusCodes.Status404NotFound);
        var err = ValidarGrupoBody(body);
        if (err != null) return FailResponse(err);

        var g = new ProductoOpcionGrupo
        {
            ServicioId = productoId,
            Nombre = body.Nombre.Trim(),
            Orden = body.Orden,
            Obligatorio = body.Obligatorio,
            MinSeleccion = body.MinSeleccion,
            MaxSeleccion = body.MaxSeleccion,
            ReemplazaPrecioBase = body.ReemplazaPrecioBase,
            Activo = body.Activo
        };
        _context.ProductoOpcionGrupos.Add(g);
        _context.SaveChanges();
        return OkResponse(new { g.Id }, "Grupo creado");
    }

    [HttpPut("grupos/{grupoId:int}")]
    public IActionResult ActualizarGrupo(int productoId, int grupoId, [FromBody] ProductoOpcionGrupoUpsertRequest body)
    {
        var g = _context.ProductoOpcionGrupos.FirstOrDefault(x => x.Id == grupoId && x.ServicioId == productoId);
        if (g == null) return FailResponse("Grupo no encontrado.", StatusCodes.Status404NotFound);
        var err = ValidarGrupoBody(body);
        if (err != null) return FailResponse(err);

        g.Nombre = body.Nombre.Trim();
        g.Orden = body.Orden;
        g.Obligatorio = body.Obligatorio;
        g.MinSeleccion = body.MinSeleccion;
        g.MaxSeleccion = body.MaxSeleccion;
        g.ReemplazaPrecioBase = body.ReemplazaPrecioBase;
        g.Activo = body.Activo;
        _context.SaveChanges();
        return OkResponse(new { g.Id }, "Grupo actualizado");
    }

    [HttpDelete("grupos/{grupoId:int}")]
    public IActionResult EliminarGrupo(int productoId, int grupoId)
    {
        var g = _context.ProductoOpcionGrupos
            .Include(x => x.Opciones)
            .FirstOrDefault(x => x.Id == grupoId && x.ServicioId == productoId);
        if (g == null) return FailResponse("Grupo no encontrado.", StatusCodes.Status404NotFound);

        _context.ProductoOpcionItems.RemoveRange(g.Opciones);
        _context.ProductoOpcionGrupos.Remove(g);
        _context.SaveChanges();
        return OkResponse<object?>(null, "Grupo eliminado");
    }

    [HttpPost("grupos/{grupoId:int}/items")]
    public IActionResult CrearOpcion(int productoId, int grupoId, [FromBody] ProductoOpcionItemUpsertRequest body)
    {
        var g = _context.ProductoOpcionGrupos.FirstOrDefault(x => x.Id == grupoId && x.ServicioId == productoId);
        if (g == null) return FailResponse("Grupo no encontrado.", StatusCodes.Status404NotFound);
        var err = ValidarItemBody(body);
        if (err != null) return FailResponse(err);

        var o = new ProductoOpcionItem
        {
            GrupoId = grupoId,
            Nombre = body.Nombre.Trim(),
            Orden = body.Orden,
            PrecioAdicional = Math.Round(body.PrecioAdicional, 2, MidpointRounding.AwayFromZero),
            Activo = body.Activo
        };
        _context.ProductoOpcionItems.Add(o);
        _context.SaveChanges();
        return OkResponse(new { o.Id }, "Opción creada");
    }

    [HttpPut("grupos/{grupoId:int}/items/{opcionId:int}")]
    public IActionResult ActualizarOpcion(int productoId, int grupoId, int opcionId, [FromBody] ProductoOpcionItemUpsertRequest body)
    {
        var o = _context.ProductoOpcionItems
            .Include(x => x.Grupo)
            .FirstOrDefault(x => x.Id == opcionId && x.GrupoId == grupoId && x.Grupo.ServicioId == productoId);
        if (o == null) return FailResponse("Opción no encontrada.", StatusCodes.Status404NotFound);
        var err = ValidarItemBody(body);
        if (err != null) return FailResponse(err);

        o.Nombre = body.Nombre.Trim();
        o.Orden = body.Orden;
        o.PrecioAdicional = Math.Round(body.PrecioAdicional, 2, MidpointRounding.AwayFromZero);
        o.Activo = body.Activo;
        _context.SaveChanges();
        return OkResponse(new { o.Id }, "Opción actualizada");
    }

    [HttpDelete("grupos/{grupoId:int}/items/{opcionId:int}")]
    public IActionResult EliminarOpcion(int productoId, int grupoId, int opcionId)
    {
        var o = _context.ProductoOpcionItems
            .Include(x => x.Grupo)
            .FirstOrDefault(x => x.Id == opcionId && x.GrupoId == grupoId && x.Grupo.ServicioId == productoId);
        if (o == null) return FailResponse("Opción no encontrada.", StatusCodes.Status404NotFound);

        if (_context.FacturaServicioOpcionesSeleccion.Any(s => s.ProductoOpcionItemId == opcionId))
            return FailResponse("No se puede eliminar: hay líneas de pedido que usan esta opción. Desactívela (activo=false).", StatusCodes.Status409Conflict);

        _context.ProductoOpcionItems.Remove(o);
        _context.SaveChanges();
        return OkResponse<object?>(null, "Opción eliminada");
    }

    private bool ExisteProducto(int id) => _context.Servicios.Any(s => s.Id == id);

    private static string? ValidarGrupoBody(ProductoOpcionGrupoUpsertRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Nombre)) return "Nombre del grupo es requerido.";
        if (body.MinSeleccion < 0) return "MinSeleccion no puede ser negativo.";
        if (body.MaxSeleccion < 0) return "MaxSeleccion no puede ser negativo; use 0 para sin límite superior.";
        if (body.MaxSeleccion > 0 && body.MaxSeleccion < body.MinSeleccion) return "MaxSeleccion debe ser >= MinSeleccion.";
        return null;
    }

    private static string? ValidarItemBody(ProductoOpcionItemUpsertRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Nombre)) return "Nombre de la opción es requerido.";
        if (body.PrecioAdicional < 0) return "Precio adicional no puede ser negativo.";
        return null;
    }
}

public class ProductoOpcionGrupoUpsertRequest
{
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Obligatorio { get; set; }
    public int MinSeleccion { get; set; }
    /// <summary>0 = sin máximo.</summary>
    public int MaxSeleccion { get; set; } = 1;
    public bool ReemplazaPrecioBase { get; set; } = false;
    public bool Activo { get; set; } = true;
}

public class ProductoOpcionItemUpsertRequest
{
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public decimal PrecioAdicional { get; set; }
    public bool Activo { get; set; } = true;
}
