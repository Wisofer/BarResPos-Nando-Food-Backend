using BarRestPOS.Data;
using BarRestPOS.Services;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Controllers.Api.V1;

[ApiController]
[AllowAnonymous]
[Route("api/v1/public/facturas")]
public class PublicFacturasApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPdfService _pdfService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PublicFacturasApiController> _logger;

    public PublicFacturasApiController(
        ApplicationDbContext context,
        IPdfService pdfService,
        IConfiguration configuration,
        ILogger<PublicFacturasApiController> logger)
    {
        _context = context;
        _pdfService = pdfService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("{pedidoId:int}/pdf")]
    public IActionResult DescargarPdfPublico(int pedidoId, [FromQuery] string? token)
    {
        if (!PdfTokenHelper.ValidarTokenTemporal(pedidoId, token, _configuration))
            return Unauthorized("Token inválido o expirado.");

        var pedido = _context.Facturas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(f => f.Mesa)
            .Include(f => f.Cliente)
            .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
            .FirstOrDefault(f => f.Id == pedidoId);

        if (pedido == null)
            return NotFound("Pedido no encontrado.");

        try
        {
            var bytes = _pdfService.GenerarPdfPedido(pedido);
            var nombre = $"pedido_{pedido.Numero}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            return File(bytes, "application/pdf", nombre);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar PDF público para pedido {PedidoId}", pedidoId);
            return StatusCode(StatusCodes.Status500InternalServerError, "Error al generar PDF.");
        }
    }
}

