using BarRestPOS.Data;
using BarRestPOS.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Controllers.Api.V1;

/// <summary>
/// HTML para tickets (cocina, comanda, recibo). El cliente puede abrir en iframe;
/// si no se envía header Authorization, usar query <c>?access_token=...</c> (JWT).
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

    [HttpGet("cocina/{ordenId:int}")]
    public IActionResult TicketCocina(int ordenId)
    {
        try
        {
            var orden = _context.Facturas
                .AsSplitQuery()
                .Include(f => f.Mesa)
                .Include(f => f.Mesero)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
                .ThenInclude(s => s.CategoriaProducto)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
                .FirstOrDefault(f => f.Id == ordenId);

            if (orden == null)
                return NotFound("Orden no encontrada");

            var html = _impresionService.GenerarTicketCocina(orden);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar ticket de cocina");
            return BadRequest("Error al generar el ticket");
        }
    }

    [HttpGet("recibo/{pagoId:int}")]
    public IActionResult TicketRecibo(int pagoId)
    {
        try
        {
            var pago = _context.Pagos
                .AsSplitQuery()
                .Include(p => p.Factura)
                .ThenInclude(f => f.Mesa)
                .Include(p => p.Factura)
                .ThenInclude(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
                .Include(p => p.Factura)
                .ThenInclude(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
                .FirstOrDefault(p => p.Id == pagoId);

            if (pago == null || pago.Factura == null)
                return NotFound("Pago no encontrado");

            var html = _impresionService.GenerarTicketRecibo(pago, pago.Factura);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar ticket de recibo");
            return BadRequest("Error al generar el ticket");
        }
    }

    [HttpGet("comanda/{ordenId:int}")]
    public IActionResult TicketComanda(int ordenId)
    {
        try
        {
            var orden = _context.Facturas
                .AsSplitQuery()
                .Include(f => f.Mesa)
                .Include(f => f.Mesero)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
                .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
                .FirstOrDefault(f => f.Id == ordenId);

            if (orden == null)
                return NotFound("Orden no encontrada");

            var html = _impresionService.GenerarTicketComanda(orden);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar ticket de comanda");
            return BadRequest("Error al generar el ticket");
        }
    }
}
