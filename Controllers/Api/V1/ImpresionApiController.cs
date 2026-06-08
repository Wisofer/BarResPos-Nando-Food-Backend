using BarRestPOS.Data;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace BarRestPOS.Controllers.Api.V1;

/// <summary>
/// API de Impresión Nativa. Envia bytes ESC/POS directamente a la impresora configurada.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/impresion")]
public class ImpresionApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IImpresionService _impresionService;
    private readonly ILogger<ImpresionApiController> _logger;

    public ImpresionApiController(
        ApplicationDbContext context,
        IImpresionService impresionService,
        ILogger<ImpresionApiController> logger)
    {
        _context = context;
        _impresionService = impresionService;
        _logger = logger;
    }

    private string ObtenerNombreImpresora(string claveConf, string fallback = "")
    {
        var nombre = _context.Configuraciones.AsNoTracking().FirstOrDefault(c => c.Clave == claveConf)?.Valor;
        return string.IsNullOrWhiteSpace(nombre) ? fallback : nombre.Trim();
    }

    private System.Collections.Generic.List<int>? ParseLineas(string? lineas)
    {
        if (string.IsNullOrWhiteSpace(lineas)) return null;
        return lineas.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .ToList();
    }

    [Authorize(Policy = "Cocina")]
    [HttpPost("cocina/{ordenId:int}")]
    public IActionResult TicketCocina(int ordenId, [FromQuery] string? lineas)
    {
        try
        {
            var orden = _context.Facturas
                .AsSplitQuery()
                .Include(f => f.Mesa)
                    .ThenInclude(m => m.Ubicacion)
                .Include(f => f.Mesero)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
                .ThenInclude(s => s.CategoriaProducto)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
                .FirstOrDefault(f => f.Id == ordenId);

            if (orden == null)
                return NotFound(new { mensaje = "Orden no encontrada" });

            var lineasFilter = ParseLineas(lineas);
            var bytes = _impresionService.GenerarTicketCocina(orden, lineasFilter);
            var printerName = ObtenerNombreImpresora("Tickets:ImpresoraCocina", "Cocina");

            bool ok = RawPrinterHelper.SendBytesToPrinter(printerName, bytes, $"Cocina-{orden.Numero}");
            
            if (!ok) return BadRequest(new { mensaje = $"Error al imprimir. Verifique impresora: {printerName}" });
            return Ok(new { mensaje = "Ticket enviado a cocina", impresora = printerName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar ticket de cocina");
            return BadRequest(new { mensaje = "Error interno al imprimir" });
        }
    }

    [Authorize(Policy = "Cocina")]
    [HttpPost("bar/{ordenId:int}")]
    public IActionResult TicketBar(int ordenId, [FromQuery] string? lineas)
    {
        try
        {
            var orden = _context.Facturas
                .AsSplitQuery()
                .Include(f => f.Mesa)
                    .ThenInclude(m => m.Ubicacion)
                .Include(f => f.Mesero)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
                .ThenInclude(s => s.CategoriaProducto)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
                .FirstOrDefault(f => f.Id == ordenId);

            if (orden == null)
                return NotFound(new { mensaje = "Orden no encontrada" });

            var lineasFilter = ParseLineas(lineas);
            var bytes = _impresionService.GenerarTicketBar(orden, lineasFilter);
            var printerName = ObtenerNombreImpresora("Tickets:ImpresoraBar", "Bar");

            bool ok = RawPrinterHelper.SendBytesToPrinter(printerName, bytes, $"Bar-{orden.Numero}");
            
            if (!ok) return BadRequest(new { mensaje = $"Error al imprimir. Verifique impresora: {printerName}" });
            return Ok(new { mensaje = "Ticket enviado a bar", impresora = printerName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar ticket de bar");
            return BadRequest(new { mensaje = "Error interno al imprimir" });
        }
    }

    [Authorize(Policy = "Cajero")]
    [HttpPost("recibo/{pagoId:int}")]
    public IActionResult TicketRecibo(int pagoId)
    {
        try
        {
            var pago = _context.Pagos
                .AsSplitQuery()
                .Include(p => p.Factura)
                .ThenInclude(f => f.Mesa)
                .ThenInclude(m => m.Ubicacion)
                .Include(p => p.Factura)
                .ThenInclude(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
                .Include(p => p.Factura)
                .ThenInclude(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
                .FirstOrDefault(p => p.Id == pagoId);

            if (pago == null || pago.Factura == null)
                return NotFound(new { mensaje = "Pago no encontrado" });

            var bytes = _impresionService.GenerarTicketRecibo(pago, pago.Factura);
            var printerName = ObtenerNombreImpresora("Tickets:ImpresoraCaja", "Caja");

            bool ok = RawPrinterHelper.SendBytesToPrinter(printerName, bytes, $"Recibo-{pago.Factura.Numero}");
            
            if (!ok) return BadRequest(new { mensaje = $"Error al imprimir. Verifique impresora: {printerName}" });
            return Ok(new { mensaje = "Recibo impreso con éxito", impresora = printerName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar ticket de recibo");
            return BadRequest(new { mensaje = "Error interno al imprimir" });
        }
    }

    [Authorize]
    [HttpPost("comanda/{ordenId:int}")]
    public IActionResult TicketComanda(int ordenId)
    {
        try
        {
            var orden = _context.Facturas
                .AsSplitQuery()
                .Include(f => f.Mesa)
                    .ThenInclude(m => m.Ubicacion)
                .Include(f => f.Mesero)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
                .FirstOrDefault(f => f.Id == ordenId);

            if (orden == null)
                return NotFound(new { mensaje = "Orden no encontrada" });

            var bytes = _impresionService.GenerarTicketComanda(orden);
            var printerName = ObtenerNombreImpresora("Tickets:ImpresoraComanda", ObtenerNombreImpresora("Tickets:ImpresoraCaja", "Caja"));

            bool ok = RawPrinterHelper.SendBytesToPrinter(printerName, bytes, $"Comanda-{orden.Numero}");
            
            if (!ok) return BadRequest(new { mensaje = $"Error al imprimir. Verifique impresora: {printerName}" });
            return Ok(new { mensaje = "Comanda impresa con éxito", impresora = printerName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar ticket de comanda");
            return BadRequest(new { mensaje = "Error interno al imprimir" });
        }
    }

    [Authorize]
    [HttpGet("comanda/{ordenId:int}/preview")]
    public IActionResult PreviewComanda(int ordenId)
    {
        try
        {
            var orden = _context.Facturas
                .AsNoTracking()
                .AsSplitQuery()
                .Include(f => f.Mesa)
                    .ThenInclude(m => m.Ubicacion)
                .Include(f => f.Mesero)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
                .FirstOrDefault(f => f.Id == ordenId);

            if (orden == null)
                return NotFound(new { mensaje = "Orden no encontrada" });

            var texto = _impresionService.GenerarPreviewComanda(orden);
            return Ok(new { preview = texto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar preview de comanda");
            return BadRequest(new { mensaje = "Error interno al generar previsualización" });
        }
    }

    [Authorize(Policy = "Cajero")]
    [HttpGet("recibo/{pagoId:int}/preview")]
    public IActionResult PreviewRecibo(int pagoId)
    {
        try
        {
            var pago = _context.Pagos
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.Factura)
                .ThenInclude(f => f.Mesa)
                .ThenInclude(m => m.Ubicacion)
                .Include(p => p.Factura)
                .ThenInclude(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
                .Include(p => p.Factura)
                .ThenInclude(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
                .FirstOrDefault(p => p.Id == pagoId);

            if (pago == null || pago.Factura == null)
                return NotFound(new { mensaje = "Pago no encontrado" });

            var texto = _impresionService.GenerarPreviewRecibo(pago, pago.Factura);
            return Ok(new { preview = texto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar preview de recibo");
            return BadRequest(new { mensaje = "Error interno al generar previsualización" });
        }
    }

    [Authorize(Policy = "Cocina")]
    [HttpGet("cocina/{ordenId:int}/preview")]
    public IActionResult PreviewCocina(int ordenId, [FromQuery] string? lineas)
    {
        try
        {
            var orden = _context.Facturas
                .AsNoTracking()
                .AsSplitQuery()
                .Include(f => f.Mesa)
                    .ThenInclude(m => m.Ubicacion)
                .Include(f => f.Mesero)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
                .FirstOrDefault(f => f.Id == ordenId);

            if (orden == null)
                return NotFound(new { mensaje = "Orden no encontrada" });

            var lineasFilter = ParseLineas(lineas);
            var texto = _impresionService.GenerarPreviewCocina(orden, lineasFilter);
            return Ok(new { preview = texto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar preview de cocina");
            return BadRequest(new { mensaje = "Error interno al generar previsualización" });
        }
    }

    [Authorize(Policy = "Cocina")]
    [HttpGet("bar/{ordenId:int}/preview")]
    public IActionResult PreviewBar(int ordenId, [FromQuery] string? lineas)
    {
        try
        {
            var orden = _context.Facturas
                .AsNoTracking()
                .AsSplitQuery()
                .Include(f => f.Mesa)
                    .ThenInclude(m => m.Ubicacion)
                .Include(f => f.Mesero)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
                .FirstOrDefault(f => f.Id == ordenId);

            if (orden == null)
                return NotFound(new { mensaje = "Orden no encontrada" });

            var lineasFilter = ParseLineas(lineas);
            var texto = _impresionService.GenerarPreviewBar(orden, lineasFilter);
            return Ok(new { preview = texto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar preview de bar");
            return BadRequest(new { mensaje = "Error interno al generar previsualización" });
        }
    }
}
