using BarRestPOS.Data;
using BarRestPOS.Models.Api;
using BarRestPOS.Services.IServices;
using BarRestPOS.Services;
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
    private readonly PrinterQueueManager _queueManager;

    public ImpresionApiController(
        ApplicationDbContext context,
        IImpresionService impresionService,
        ILogger<ImpresionApiController> logger,
        PrinterQueueManager queueManager)
    {
        _context = context;
        _impresionService = impresionService;
        _logger = logger;
        _queueManager = queueManager;
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
    public async Task<IActionResult> TicketCocina(int ordenId, [FromQuery] string? lineas)
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

            bool ok = await _queueManager.RunSerializedAsync(printerName, () =>
                RawPrinterHelper.SendBytesToPrinter(printerName, bytes, $"Cocina-{orden.Numero}"));
            
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
    public async Task<IActionResult> TicketBar(int ordenId, [FromQuery] string? lineas)
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

            bool ok = await _queueManager.RunSerializedAsync(printerName, () =>
                RawPrinterHelper.SendBytesToPrinter(printerName, bytes, $"Bar-{orden.Numero}"));
            
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
    public async Task<IActionResult> TicketRecibo(int pagoId)
    {
        try
        {
            var pago = _context.Pagos
                .AsSplitQuery()
                .Include(p => p.Factura)
                .ThenInclude(f => f.Mesa)
                .ThenInclude(m => m.Ubicacion)
                .Include(p => p.Factura)
                .ThenInclude(f => f.Mesero)
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

            bool ok = await _queueManager.RunSerializedAsync(printerName, () =>
                RawPrinterHelper.SendBytesToPrinter(printerName, bytes, $"Recibo-{pago.Factura.Numero}"));
            
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
    public async Task<IActionResult> TicketComanda(int ordenId)
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

            bool ok = await _queueManager.RunSerializedAsync(printerName, () =>
                RawPrinterHelper.SendBytesToPrinter(printerName, bytes, $"Comanda-{orden.Numero}"));
            
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
                .ThenInclude(f => f.Mesero)
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

    [Authorize(Policy = "Cajero")]
    [HttpPost("corte/{cierreId:int}")]
    public async Task<IActionResult> TicketCorte(int cierreId, [FromServices] ICajaService cajaService)
    {
        try
        {
            var cierre = await cajaService.ObtenerCierrePorIdAsync(cierreId);
            if (cierre == null) return NotFound(new { mensaje = "Cierre no encontrado" });

            var bytes = _impresionService.GenerarTicketCorte(cierre);
            var printerName = ObtenerNombreImpresora("Tickets:ImpresoraCaja", "Caja");

            bool ok = await _queueManager.RunSerializedAsync(printerName, () =>
                RawPrinterHelper.SendBytesToPrinter(printerName, bytes, $"Corte-{cierre.Id}"));
            
            if (!ok) return BadRequest(new { mensaje = $"Error al imprimir. Verifique impresora: {printerName}" });
            return Ok(new { mensaje = "Corte de caja impreso con éxito", impresora = printerName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar ticket de corte de caja");
            return BadRequest(new { mensaje = "Error interno al imprimir" });
        }
    }

    [Authorize(Policy = "Cajero")]
    [HttpGet("corte/{cierreId:int}/preview")]
    public async Task<IActionResult> PreviewCorte(int cierreId, [FromServices] ICajaService cajaService)
    {
        try
        {
            var cierre = await cajaService.ObtenerCierrePorIdAsync(cierreId);
            if (cierre == null) return NotFound(new { mensaje = "Cierre no encontrado" });

            var texto = _impresionService.GenerarPreviewCorte(cierre);
            return Ok(new { preview = texto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar preview de corte");
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

    [HttpPost("cancelacion-linea/{facturaId:int}/{lineaId:int}")]
    public async Task<IActionResult> CancelarLinea(
        int facturaId,
        int lineaId,
        [FromBody] CancelarPedidoRequest? request,
        [FromServices] IConfiguracionService configuracionService,
        [FromServices] IInventarioService inventarioService,
        [FromServices] IAuditService auditService)
    {
        try
        {
            var pin = configuracionService.ObtenerValor(SD.ConfigClavePinCancelacionPedidos)?.Trim();
            if (string.IsNullOrEmpty(pin))
                return BadRequest(new { mensaje = "Configure el PIN de cancelación en Configuraciones (PinCancelacionPedidos)." });

            var codigo = request?.Codigo?.Trim();
            if (string.IsNullOrEmpty(codigo))
                return BadRequest(new { mensaje = "El código de verificación es requerido." });

            if (!string.Equals(codigo, pin, StringComparison.Ordinal))
                return StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "Código de verificación inválido." });

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
                .FirstOrDefault(f => f.Id == facturaId);

            if (orden == null)
                return NotFound(new { mensaje = "Orden no encontrada." });

            if (orden.Estado == SD.EstadoOrdenPagado || orden.Estado == SD.EstadoOrdenCancelado)
                return BadRequest(new { mensaje = "No se puede editar un pedido pagado o cancelado." });

            var linea = orden.FacturaServicios.FirstOrDefault(fs => fs.Id == lineaId);
            if (linea == null)
                return NotFound(new { mensaje = "Producto no encontrado en esta orden." });

            var userId = SecurityHelper.GetUserId(User);
            if (!userId.HasValue)
                return Unauthorized(new { mensaje = "Usuario no autenticado." });

            bool esCocina = CocinaCatalogoHelper.FacturaServicioRequiereCocina(linea);
            string printerName = esCocina
                ? ObtenerNombreImpresora("Tickets:ImpresoraCocina", "Cocina")
                : ObtenerNombreImpresora("Tickets:ImpresoraBar", "Bar");

            var bytes = _impresionService.GenerarTicketCancelacionItem(orden, linea);
            bool printOk = await _queueManager.RunSerializedAsync(printerName, () =>
                RawPrinterHelper.SendBytesToPrinter(printerName, bytes, $"Cancelacion-{orden.Numero}-{linea.Id}"));
            if (!printOk)
            {
                _logger.LogWarning("No se pudo imprimir el ticket de cancelación en la impresora: {PrinterName}", printerName);
            }

            var mesaIdAlInicio = orden.MesaId;

            using var tx = _context.Database.BeginTransaction();
            try
            {
                var svc = _context.Servicios.FirstOrDefault(s => s.Id == linea.ServicioId);
                if (svc != null && svc.ControlarStock && linea.Cantidad > 0)
                {
                    var refPedido = string.IsNullOrWhiteSpace(orden.Numero) ? $"#{orden.Id}" : orden.Numero;
                    inventarioService.RegistrarEntrada(
                        linea.ServicioId,
                        linea.Cantidad,
                        null,
                        null,
                        null,
                        $"Devolución por cancelar producto — pedido {refPedido}",
                        userId.Value);
                }

                if (linea.OpcionesSeleccionadas?.Count > 0)
                    _context.FacturaServicioOpcionesSeleccion.RemoveRange(linea.OpcionesSeleccionadas);

                _context.FacturaServicios.Remove(linea);
                orden.FacturaServicios.Remove(linea);

                bool vacio = false;
                if (orden.FacturaServicios.Count == 0)
                {
                    orden.Monto = 0;
                    orden.Estado = SD.EstadoOrdenGuardado;
                    orden.EstadoCocina = SD.EstadoCocinaPendiente;
                    orden.MesaId = null;
                    orden.FechaActualizacion = DateTime.Now;
                    vacio = true;
                }
                else
                {
                    orden.Monto = Math.Round(
                        orden.FacturaServicios.Sum(fs => fs.Monto),
                        2,
                        MidpointRounding.AwayFromZero);
                    orden.ServicioId = orden.FacturaServicios.First().ServicioId;
                    orden.FechaActualizacion = DateTime.Now;
                }

                if (vacio && mesaIdAlInicio.HasValue)
                {
                    var mesa = _context.Mesas.FirstOrDefault(m => m.Id == mesaIdAlInicio.Value);
                    if (mesa != null)
                    {
                        var otrosEnOrigen = _context.Facturas.Count(f =>
                            f.MesaId == mesaIdAlInicio.Value
                            && f.Id != orden.Id
                            && f.Estado != SD.EstadoOrdenPagado
                            && f.Estado != SD.EstadoOrdenCancelado);
                        if (otrosEnOrigen == 0)
                        {
                            mesa.Estado = SD.EstadoMesaLibre;
                        }
                    }
                }

                _context.SaveChanges();
                tx.Commit();

                // Registrar auditoría de cancelación de línea con PIN
                try
                {
                    await auditService.RegistrarAccionAsync(
                        "CancelacionLineaConPin",
                        orden.Mesa?.Numero ?? (orden.MesaId.HasValue ? $"Mesa {orden.MesaId.Value}" : "Delivery/Llevar"),
                        orden.Id,
                        new { producto = linea.Servicio?.Nombre ?? $"ID: {linea.ServicioId}", cantidad = linea.Cantidad, monto = linea.Monto }
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al registrar auditoria de cancelacion de linea");
                }

                return Ok(new
                {
                    id = orden.Id,
                    monto = orden.Monto,
                    estado = orden.Estado,
                    mesaId = orden.MesaId,
                    vacio = vacio,
                    mensaje = "Producto cancelado y aviso impreso correctamente."
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Error al eliminar producto {LineaId} de la orden {OrdenId}", lineaId, facturaId);
                return BadRequest(new { mensaje = "Error al procesar la cancelación del producto." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error general en la cancelación del producto");
            return BadRequest(new { mensaje = "Error interno del servidor." });
        }
    }
}
