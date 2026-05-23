using BarRestPOS.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize(Policy = "Administrador")]
[Route("api/v1/reportes")]
public class ReportesApiController : BaseApiController
{
    private readonly IReporteService _reporteService;

    public ReportesApiController(IReporteService reporteService)
    {
        _reporteService = reporteService;
    }

    [HttpGet("resumen-ventas")]
    public async Task<IActionResult> ResumenVentas([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] bool exportar = false, [FromQuery] string? filtroVentas = "activas")
    {
        try
        {
            if (exportar)
            {
                var detalle = await _reporteService.ObtenerDetalleVentasAsync(desde, hasta, filtroVentas);
                var fDesde = desde ?? DateTime.Today;
                var fHasta = hasta ?? DateTime.Today;
                var bytes = _reporteService.GenerarExcelVentas(fDesde, fHasta, detalle);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"reporte_ventas_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            var resumen = await _reporteService.ObtenerResumenVentasAsync(desde, hasta);
            return OkResponse(resumen);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("resumen-ventas/detalle")]
    public async Task<IActionResult> ResumenVentasDetalle([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] string? filtroVentas = "activas")
    {
        try
        {
            var items = await _reporteService.ObtenerDetalleVentasAsync(desde, hasta, filtroVentas);
            return OkResponse(new
            {
                desde = desde ?? DateTime.Today,
                hasta = hasta ?? DateTime.Today,
                filtroVentas = filtroVentas ?? "activas",
                total = items.Count,
                items
            });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("ventas/{id:int}/ticket-detalle")]
    public async Task<IActionResult> TicketDetalle(int id)
    {
        try
        {
            var ticket = await _reporteService.ObtenerTicketCompletoPorOrdenIdAsync(id);
            if (ticket == null) return FailResponse("Orden no encontrada o estado no disponible para este reporte.", StatusCodes.Status404NotFound);
            return OkResponse(ticket);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("ventas-por-mesero")]
    public async Task<IActionResult> VentasPorMesero([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] bool exportar = false)
    {
        try
        {
            var items = await _reporteService.ObtenerVentasPorMeseroAsync(desde, hasta);
            if (exportar)
            {
                var fDesde = desde ?? DateTime.Today.AddDays(-30);
                var fHasta = hasta ?? DateTime.Today;
                var bytes = _reporteService.GenerarExcelVentasPorMesero(fDesde, fHasta, items);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ventas_por_mesero_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            return OkResponse(new { desde = desde ?? DateTime.Today.AddDays(-30), hasta = hasta ?? DateTime.Today, total = items.Count, items });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("ventas-por-categoria")]
    public async Task<IActionResult> VentasPorCategoria([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] bool exportar = false)
    {
        try
        {
            var items = await _reporteService.ObtenerVentasPorCategoriaAsync(desde, hasta);
            if (exportar)
            {
                var fDesde = desde ?? DateTime.Today.AddDays(-30);
                var fHasta = hasta ?? DateTime.Today;
                var bytes = _reporteService.GenerarExcelCategorias(fDesde, fHasta, items);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ventas_por_categoria_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            return OkResponse(new { desde = desde ?? DateTime.Today.AddDays(-30), hasta = hasta ?? DateTime.Today, total = items.Count, items });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("ventas-por-categoria/desglose")]
    public async Task<IActionResult> VentasPorCategoriaDesglose([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] bool exportar = false)
    {
        try
        {
            var items = await _reporteService.ObtenerVentasPorCategoriaConDesgloseAsync(desde, hasta);
            if (exportar)
            {
                var fDesde = desde ?? DateTime.Today.AddDays(-30);
                var fHasta = hasta ?? DateTime.Today;
                var bytes = _reporteService.GenerarExcelCategoriasConDesglose(fDesde, fHasta, items);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ventas_por_categoria_desglose_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            return OkResponse(new { desde = desde ?? DateTime.Today.AddDays(-30), hasta = hasta ?? DateTime.Today, totalCategorias = items.Count, items });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("productos-top")]
    public async Task<IActionResult> ProductosTop([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int top = 10, [FromQuery] bool exportar = false)
    {
        try
        {
            var items = await _reporteService.ObtenerProductosTopAsync(desde, hasta, top);
            if (exportar)
            {
                var fDesde = desde ?? DateTime.Today.AddDays(-30);
                var fHasta = hasta ?? DateTime.Today;
                var bytes = _reporteService.GenerarExcelTopProductos(fDesde, fHasta, items);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"top_productos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            return OkResponse(items);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    // Rutas de compatibilidad con frontend actual
    [HttpGet("resumen-ventas/excel")]
    public Task<IActionResult> ResumenVentasExcel([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        => ResumenVentas(desde, hasta, exportar: true, filtroVentas: "activas");

    [HttpGet("productos-top/excel")]
    public Task<IActionResult> ProductosTopExcel([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int top = 10)
        => ProductosTop(desde, hasta, top, exportar: true);

    [HttpGet("ventas-por-mesero/excel")]
    public Task<IActionResult> VentasPorMeseroExcel([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        => VentasPorMesero(desde, hasta, exportar: true);

    [HttpGet("ventas-por-categoria/excel")]
    public Task<IActionResult> VentasPorCategoriaExcel([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        => VentasPorCategoria(desde, hasta, exportar: true);
}
