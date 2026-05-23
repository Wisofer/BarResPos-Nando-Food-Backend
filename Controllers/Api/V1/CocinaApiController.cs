using BarRestPOS.Data;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/cocina")]
public class CocinaApiController : BaseApiController
{
    private readonly ApplicationDbContext _context;

    public CocinaApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("ordenes")]
    public IActionResult Ordenes([FromQuery] string? estadoCocina)
    {
        var query = _context.Facturas
            .AsNoTracking()
            .Include(f => f.Mesa)
            .Include(f => f.Mesero)
            .Include(f => f.FacturaServicios).ThenInclude(i => i.Servicio).ThenInclude(s => s.CategoriaProducto)
            .Include(f => f.FacturaServicios).ThenInclude(i => i.OpcionesSeleccionadas)
            .Where(f => f.Estado != SD.EstadoOrdenPagado && f.Estado != SD.EstadoOrdenCancelado)
            .Where(f => f.FacturaServicios.Any(i =>
                i.Servicio.CategoriaProducto == null || i.Servicio.CategoriaProducto.RequiereCocina));

        if (!string.IsNullOrWhiteSpace(estadoCocina))
        {
            query = query.Where(f => f.EstadoCocina == estadoCocina);
        }

        var items = query.OrderBy(f => f.FechaCreacion).ToList()
            .Select(f => new
            {
                f.Id,
                f.Numero,
                f.OrigenPedido,
                f.Estado,
                f.EstadoCocina,
                f.FechaCreacion,
                Mesa = f.Mesa != null ? f.Mesa.Numero : "S/M",
                Mesero = f.Mesero != null ? f.Mesero.NombreCompleto : "N/A",
                DeliveryClienteNombre = f.DeliveryClienteNombre,
                DeliveryClienteTelefono = f.DeliveryClienteTelefono,
                DeliveryClienteDireccion = f.DeliveryClienteDireccion,
                Items = CocinaCatalogoHelper.LineasCocina(f.FacturaServicios).Select(i => new
                {
                    i.Id,
                    Producto = i.Servicio.Nombre,
                    i.Cantidad,
                    i.Estado,
                    i.Notas,
                    opcionesResumen = ProductoOpcionesLineaHelper.OpcionesResumen(i.OpcionesSeleccionadas),
                    opcionesSeleccionadas = ProductoOpcionesLineaHelper.MapOpcionesLineaRespuesta(i.OpcionesSeleccionadas),
                    RequiereCocina = true
                })
            });

        return OkResponse(items);
    }

    [HttpPatch("ordenes/{id:int}/estado")]
    [Authorize(Policy = "Administrador")]
    public IActionResult CambiarEstadoOrden(int id, [FromBody] CambiarEstadoCocinaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Estado)) return FailResponse("Estado requerido.");
        var orden = _context.Facturas.FirstOrDefault(f => f.Id == id);
        if (orden == null) return FailResponse("Orden no encontrada.", StatusCodes.Status404NotFound);

        if (orden.Estado == SD.EstadoOrdenPagado || orden.Estado == SD.EstadoOrdenCancelado)
        {
            return FailResponse("No se puede cambiar estado de cocina de una orden pagada o cancelada.", StatusCodes.Status409Conflict);
        }

        var nuevoEstado = request.Estado.Trim();
        // Idempotente: si el estado es el mismo, responder OK sin efectos secundarios.
        if (orden.EstadoCocina == nuevoEstado)
        {
            return OkResponse(new { orden.Id, orden.EstadoCocina }, "Estado de cocina sin cambios");
        }

        orden.EstadoCocina = nuevoEstado;
        if (orden.EstadoCocina == SD.EstadoCocinaListo) orden.FechaListo = DateTime.Now;
        _context.SaveChanges();
        return OkResponse(new { orden.Id, orden.EstadoCocina }, "Estado de cocina actualizado");
    }

    [HttpPatch("items/{id:int}/estado")]
    [Authorize(Policy = "Administrador")]
    public IActionResult CambiarEstadoItem(int id, [FromBody] CambiarEstadoCocinaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Estado)) return FailResponse("Estado requerido.");
        var item = _context.FacturaServicios
            .Include(i => i.Servicio).ThenInclude(s => s.CategoriaProducto)
            .FirstOrDefault(i => i.Id == id);
        if (item == null) return FailResponse("Item no encontrado.", StatusCodes.Status404NotFound);
        if (!CocinaCatalogoHelper.FacturaServicioRequiereCocina(item))
            return FailResponse("Este ítem no participa en cocina.", StatusCodes.Status409Conflict);

        item.Estado = request.Estado.Trim();
        _context.SaveChanges();
        return OkResponse(new { item.Id, item.Estado }, "Estado del item actualizado");
    }
}

public class CambiarEstadoCocinaRequest
{
    public string Estado { get; set; } = string.Empty;
}
