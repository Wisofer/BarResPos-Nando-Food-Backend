using BarRestPOS.Data;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/cocina")]
public class CocinaApiController : BaseApiController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CocinaApiController> _logger;

    public CocinaApiController(ApplicationDbContext context, ILogger<CocinaApiController> logger)
    {
        _context = context;
        _logger = logger;
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
            .Where(f => 
                (f.Estado != SD.EstadoOrdenPagado && f.Estado != SD.EstadoOrdenCancelado && f.Estado != SD.EstadoOrdenPendiente && f.Estado != SD.EstadoOrdenGuardado)
                || 
                ((f.Estado == SD.EstadoOrdenPagado || f.Estado == SD.EstadoOrdenCancelado) && f.FechaCreacion >= DateTime.Now.AddHours(-24))
            )
            .Where(f => f.FacturaServicios.Any(i =>
                i.Servicio != null && (i.Servicio.CategoriaProducto == null || i.Servicio.CategoriaProducto.RequiereCocina)));

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
                    Producto = i.Servicio?.Nombre ?? "Producto eliminado",
                    i.Cantidad,
                    i.Estado,
                    i.Notas,
                    i.FechaEnvioCocina,
                    opcionesResumen = ProductoOpcionesLineaHelper.OpcionesResumen(i.OpcionesSeleccionadas),
                    opcionesSeleccionadas = ProductoOpcionesLineaHelper.MapOpcionesLineaRespuesta(i.OpcionesSeleccionadas),
                    RequiereCocina = true
                })
            });

        return OkResponse(items);
    }

    [HttpPatch("ordenes/{id:int}/estado")]
    [Authorize(Policy = "Cocina")]
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

        // Propagar estado a todos los ítems de cocina/bar de esta orden
        var itemsAfectados = _context.FacturaServicios
            .Include(i => i.Servicio).ThenInclude(s => s.CategoriaProducto)
            .Where(i => i.FacturaId == id)
            .ToList();

        foreach (var item in itemsAfectados)
        {
            if (CocinaCatalogoHelper.FacturaServicioRequiereCocina(item) || CocinaCatalogoHelper.FacturaServicioRequiereBar(item))
            {
                item.Estado = nuevoEstado;
            }
        }

        _context.SaveChanges();
        return OkResponse(new { orden.Id, orden.EstadoCocina }, "Estado de cocina actualizado");
    }

    [HttpPatch("items/{id:int}/estado")]
    [Authorize(Policy = "Cocina")]
    public IActionResult CambiarEstadoItem(int id, [FromBody] CambiarEstadoCocinaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Estado)) return FailResponse("Estado requerido.");
        var item = _context.FacturaServicios
            .Include(i => i.Servicio).ThenInclude(s => s.CategoriaProducto)
            .FirstOrDefault(i => i.Id == id);
        if (item == null) return FailResponse("Item no encontrado.", StatusCodes.Status404NotFound);
        if (!CocinaCatalogoHelper.FacturaServicioRequiereCocina(item) && !CocinaCatalogoHelper.FacturaServicioRequiereBar(item))
            return FailResponse("Este ítem no participa en cocina/bar.", StatusCodes.Status409Conflict);

        // Recalcular estado global de cocina de la orden - Buscar orden primero
        var orden = _context.Facturas
            .Include(f => f.FacturaServicios).ThenInclude(i => i.Servicio).ThenInclude(s => s.CategoriaProducto)
            .FirstOrDefault(f => f.Id == item.FacturaId);

        if (orden == null) return FailResponse("Orden no encontrada.", StatusCodes.Status404NotFound);

        if (orden.Estado == SD.EstadoOrdenPagado || orden.Estado == SD.EstadoOrdenCancelado)
        {
            return FailResponse("No se puede cambiar estado de cocina de una orden pagada o cancelada.", StatusCodes.Status409Conflict);
        }

        item.Estado = request.Estado.Trim();
        _context.SaveChanges();

        var lineasCocinaBar = orden.FacturaServicios
            .Where(l => CocinaCatalogoHelper.FacturaServicioRequiereCocina(l) || CocinaCatalogoHelper.FacturaServicioRequiereBar(l))
            .ToList();

        if (lineasCocinaBar.Any())
        {
            if (lineasCocinaBar.All(l => l.Estado == SD.EstadoCocinaEntregado))
            {
                orden.EstadoCocina = SD.EstadoCocinaEntregado;
            }
            else if (lineasCocinaBar.All(l => l.Estado == SD.EstadoCocinaListo || l.Estado == SD.EstadoCocinaEntregado))
            {
                orden.EstadoCocina = SD.EstadoCocinaListo;
                orden.FechaListo = DateTime.Now;
            }
            else if (lineasCocinaBar.All(l => l.Estado == SD.EstadoCocinaPendiente))
            {
                orden.EstadoCocina = SD.EstadoCocinaPendiente;
            }
            else
            {
                orden.EstadoCocina = SD.EstadoCocinaEnPreparacion;
            }
            _context.SaveChanges();
        }

        return OkResponse(new { item.Id, item.Estado }, "Estado del item y de la orden actualizados");
    }

    [HttpPatch("items/estado")]
    [Authorize(Policy = "Cocina")]
    public IActionResult CambiarEstadoItemsBatch([FromBody] CambiarEstadoItemsBatchRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return FailResponse("Debe incluir al menos un item.");

        var items = _context.FacturaServicios
            .Include(i => i.Servicio).ThenInclude(s => s.CategoriaProducto)
            .Include(i => i.Factura).ThenInclude(f => f.FacturaServicios).ThenInclude(fs => fs.Servicio).ThenInclude(s => s.CategoriaProducto)
            .Where(i => request.Items.Select(r => r.Id).Contains(i.Id))
            .ToList();

        if (items.Count != request.Items.Count)
            return FailResponse("Uno o más items no fueron encontrados.", StatusCodes.Status404NotFound);

        var ordenes = items.GroupBy(i => i.Factura).ToList();
        var estadoMap = request.Items.ToDictionary(r => r.Id, r => r.Estado.Trim());

        using var tx = _context.Database.BeginTransaction();
        try
        {
            foreach (var grupo in ordenes)
            {
                var orden = grupo.Key;
                if (orden.Estado == SD.EstadoOrdenPagado || orden.Estado == SD.EstadoOrdenCancelado)
                {
                    tx.Rollback();
                    return FailResponse($"No se puede cambiar estado de cocina de la orden {orden.Numero}: está pagada o cancelada.", StatusCodes.Status409Conflict);
                }
            }

            foreach (var item in items)
            {
                if (!CocinaCatalogoHelper.FacturaServicioRequiereCocina(item) && !CocinaCatalogoHelper.FacturaServicioRequiereBar(item))
                {
                    tx.Rollback();
                    return FailResponse($"El item {item.Id} no participa en cocina/bar.", StatusCodes.Status409Conflict);
                }
                item.Estado = estadoMap[item.Id];
            }

            foreach (var grupo in ordenes)
            {
                var orden = grupo.Key;
                var lineasCocinaBar = orden.FacturaServicios
                    .Where(l => CocinaCatalogoHelper.FacturaServicioRequiereCocina(l) || CocinaCatalogoHelper.FacturaServicioRequiereBar(l))
                    .ToList();

                if (lineasCocinaBar.Any())
                {
                    if (lineasCocinaBar.All(l => l.Estado == SD.EstadoCocinaEntregado))
                    {
                        orden.EstadoCocina = SD.EstadoCocinaEntregado;
                    }
                    else if (lineasCocinaBar.All(l => l.Estado == SD.EstadoCocinaListo || l.Estado == SD.EstadoCocinaEntregado))
                    {
                        orden.EstadoCocina = SD.EstadoCocinaListo;
                        orden.FechaListo = DateTime.Now;
                    }
                    else if (lineasCocinaBar.All(l => l.Estado == SD.EstadoCocinaPendiente))
                    {
                        orden.EstadoCocina = SD.EstadoCocinaPendiente;
                    }
                    else
                    {
                        orden.EstadoCocina = SD.EstadoCocinaEnPreparacion;
                    }
                }
            }

            _context.SaveChanges();
            tx.Commit();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "Error al actualizar estado de items batch");
            return FailResponse("Error al actualizar los estados. Reintente.", StatusCodes.Status500InternalServerError);
        }

        return OkResponse(new { actualizados = items.Count }, "Estados de cocina actualizados");
    }
}

public class CambiarEstadoCocinaRequest
{
    public string Estado { get; set; } = string.Empty;
}

public class CambiarEstadoItemsBatchRequest
{
    public List<CambiarEstadoItemRequest> Items { get; set; } = new();
}

public class CambiarEstadoItemRequest
{
    public int Id { get; set; }
    public string Estado { get; set; } = string.Empty;
}
