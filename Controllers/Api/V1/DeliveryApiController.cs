using BarRestPOS.Data;
using BarRestPOS.Models.Api;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/delivery")]
public class DeliveryApiController : BaseApiController
{
    private const decimal ToleranciaMonto = 0.02m;

    private readonly ApplicationDbContext _context;
    private readonly IInventarioService _inventarioService;
    private readonly OrdenLineasReemplazoService _lineasService;
    private readonly IImpresionService _impresionService;
    private readonly PedidoCancelacionService _pedidoCancelacionService;
    private readonly IConfiguracionService _configuracionService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<DeliveryApiController> _logger;
    private readonly IConfiguration _configuration;

    public DeliveryApiController(
        ApplicationDbContext context,
        IInventarioService inventarioService,
        OrdenLineasReemplazoService lineasService,
        IImpresionService impresionService,
        PedidoCancelacionService pedidoCancelacionService,
        IConfiguracionService configuracionService,
        IWhatsAppService whatsAppService,
        ILogger<DeliveryApiController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _inventarioService = inventarioService;
        _lineasService = lineasService;
        _impresionService = impresionService;
        _pedidoCancelacionService = pedidoCancelacionService;
        _configuracionService = configuracionService;
        _whatsAppService = whatsAppService;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost("pedidos")]
    public IActionResult CrearPedido([FromBody] DeliveryGuardarPedidoRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return FailResponse("Debe incluir al menos un item.");

        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue)
            return FailResponse("Usuario no autenticado.", StatusCodes.Status401Unauthorized);

        var pedido = new Factura
        {
            Numero = GenerarNumeroDelivery(),
            MesaId = null,
            ClienteId = request.ClienteId,
            MeseroId = userId.Value,
            ServicioId = request.Items[0].ServicioId,
            Categoria = "General",
            OrigenPedido = SD.OrigenPedidoDelivery,
            DeliveryClienteNombre = request.ClienteNombre?.Trim(),
            DeliveryClienteTelefono = request.ClienteTelefono?.Trim(),
            DeliveryClienteDireccion = request.ClienteDireccion?.Trim(),
            Observaciones = request.Observaciones?.Trim(),
            Estado = SD.EstadoOrdenGuardado,
            EstadoCocina = SD.EstadoCocinaPendiente,
            FechaCreacion = DateTime.Now,
            FechaActualizacion = DateTime.Now,
            Monto = 0
        };

        _context.Facturas.Add(pedido);
        _context.SaveChanges();

        var refPedido = string.IsNullOrWhiteSpace(pedido.Numero) ? $"#{pedido.Id}" : pedido.Numero;
        var errLineas = _lineasService.ReemplazarLineas(_context, _inventarioService, pedido, request.Items, userId.Value, refPedido);
        if (errLineas != null)
            return FailResponse(errLineas, StatusCodes.Status400BadRequest);

        return OkResponse(new
        {
            id = pedido.Id,
            codigo = pedido.Numero,
            origenPedido = pedido.OrigenPedido,
            estado = pedido.Estado,
            subtotal = pedido.Monto,
            total = pedido.Monto,
            createdAt = pedido.FechaCreacion,
            updatedAt = pedido.FechaActualizacion
        }, "Pedido delivery creado");
    }

    [HttpPut("pedidos/{id:int}")]
    public IActionResult ActualizarPedido(int id, [FromBody] DeliveryGuardarPedidoRequest request)
    {
        var pedido = _context.Facturas
            .Include(f => f.FacturaServicios)
            .FirstOrDefault(f => f.Id == id && f.OrigenPedido == SD.OrigenPedidoDelivery);

        if (pedido == null) return FailResponse("Pedido delivery no encontrado.", StatusCodes.Status404NotFound);
        if (pedido.Estado == SD.EstadoOrdenPagado || pedido.Estado == SD.EstadoOrdenCancelado)
            return FailResponse("No se puede editar un pedido pagado o cancelado.");
        if (request.Items == null || request.Items.Count == 0)
            return FailResponse("Debe incluir al menos un item.");

        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue)
            return FailResponse("Usuario no autenticado.", StatusCodes.Status401Unauthorized);

        var refPedido = string.IsNullOrWhiteSpace(pedido.Numero) ? $"#{pedido.Id}" : pedido.Numero;
        var errLineas = _lineasService.ReemplazarLineas(_context, _inventarioService, pedido, request.Items, userId.Value, refPedido);
        if (errLineas != null)
            return FailResponse(errLineas, StatusCodes.Status400BadRequest);

        pedido.ClienteId = request.ClienteId;
        pedido.DeliveryClienteNombre = request.ClienteNombre?.Trim();
        pedido.DeliveryClienteTelefono = request.ClienteTelefono?.Trim();
        pedido.DeliveryClienteDireccion = request.ClienteDireccion?.Trim();
        pedido.Observaciones = request.Observaciones?.Trim();
        pedido.FechaActualizacion = DateTime.Now;

        _context.SaveChanges();

        return OkResponse(new
        {
            id = pedido.Id,
            codigo = pedido.Numero,
            origenPedido = pedido.OrigenPedido,
            estado = pedido.Estado,
            subtotal = pedido.Monto,
            total = pedido.Monto,
            createdAt = pedido.FechaCreacion,
            updatedAt = pedido.FechaActualizacion
        }, "Pedido delivery actualizado");
    }

    /// <summary>Quita una línea del pedido delivery sin reemplazar todo el carrito.</summary>
    [HttpDelete("pedidos/{pedidoId:int}/lineas/{lineaId:int}")]
    public IActionResult EliminarLinea(int pedidoId, int lineaId)
    {
        var pedido = _context.Facturas
            .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
            .FirstOrDefault(f => f.Id == pedidoId && f.OrigenPedido == SD.OrigenPedidoDelivery);

        if (pedido == null) return FailResponse("Pedido delivery no encontrado.", StatusCodes.Status404NotFound);
        if (pedido.Estado == SD.EstadoOrdenPagado || pedido.Estado == SD.EstadoOrdenCancelado)
            return FailResponse("No se puede editar un pedido pagado o cancelado.");

        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue)
            return FailResponse("Usuario no autenticado.", StatusCodes.Status401Unauthorized);

        var refPedido = string.IsNullOrWhiteSpace(pedido.Numero) ? $"#{pedido.Id}" : pedido.Numero;
        var (vacio, err) = _lineasService.EliminarLinea(_context, _inventarioService, pedido, lineaId, userId.Value, refPedido);
        if (err != null)
            return FailResponse(err, StatusCodes.Status400BadRequest);

        _context.SaveChanges();

        return OkResponse(new
        {
            id = pedido.Id,
            codigo = pedido.Numero,
            estado = pedido.Estado,
            subtotal = pedido.Monto,
            total = pedido.Monto,
            vacio
        }, vacio ? "Línea eliminada. Pedido vacío." : "Línea eliminada.");
    }

    [HttpGet("pedidos/{id:int}")]
    public IActionResult GetById(int id)
    {
        var pedido = _context.Facturas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(f => f.Mesero)
            .Include(f => f.FacturaServicios).ThenInclude(i => i.Servicio)
            .Include(f => f.FacturaServicios).ThenInclude(i => i.OpcionesSeleccionadas)
            .Where(f => f.Id == id && f.OrigenPedido == SD.OrigenPedidoDelivery)
            .FirstOrDefault();

        if (pedido == null) return FailResponse("Pedido delivery no encontrado.", StatusCodes.Status404NotFound);

        var pagos = _context.Pagos
            .AsNoTracking()
            .Include(p => p.PagoFacturas)
            .Where(p => p.FacturaId == id || p.PagoFacturas.Any(pf => pf.FacturaId == id))
            .OrderByDescending(p => p.FechaPago)
            .ToList();

        return OkResponse(MapPedidoDetalle(pedido, pagos));
    }

    [HttpGet("pedidos")]
    public IActionResult Listar(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] string? estado,
        [FromQuery] string? cliente,
        [FromQuery] string? telefono,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _context.Facturas
            .AsNoTracking()
            .Where(f => f.OrigenPedido == SD.OrigenPedidoDelivery)
            .AsQueryable();

        if (desde.HasValue) query = query.Where(f => f.FechaCreacion >= desde.Value);
        if (hasta.HasValue) query = query.Where(f => f.FechaCreacion <= hasta.Value);
        if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(f => f.Estado == estado.Trim());
        if (!string.IsNullOrWhiteSpace(cliente))
        {
            var c = cliente.Trim().ToLower();
            query = query.Where(f => (f.DeliveryClienteNombre ?? "").ToLower().Contains(c));
        }
        if (!string.IsNullOrWhiteSpace(telefono))
        {
            var t = telefono.Trim();
            query = query.Where(f => (f.DeliveryClienteTelefono ?? "").Contains(t));
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim().ToLower();
            query = query.Where(f =>
                (f.Numero ?? "").ToLower().Contains(s)
                || (f.DeliveryClienteNombre ?? "").ToLower().Contains(s)
                || (f.DeliveryClienteTelefono ?? "").Contains(s));
        }

        var total = query.Count();
        var items = query
            .OrderByDescending(f => f.FechaCreacion)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new
            {
                id = f.Id,
                codigo = f.Numero,
                origenPedido = f.OrigenPedido,
                estado = f.Estado,
                estadoCocina = f.EstadoCocina,
                clienteNombre = f.DeliveryClienteNombre,
                clienteTelefono = f.DeliveryClienteTelefono,
                clienteDireccion = f.DeliveryClienteDireccion,
                observaciones = f.Observaciones,
                subtotal = f.Monto,
                total = f.Monto,
                createdAt = f.FechaCreacion,
                updatedAt = f.FechaActualizacion
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

    [HttpPatch("pedidos/{id:int}/enviar-cocina")]
    public IActionResult EnviarCocina(int id)
    {
        var pedido = _context.Facturas
            .Include(f => f.FacturaServicios)
            .ThenInclude(l => l.Servicio)
            .ThenInclude(s => s!.CategoriaProducto)
            .FirstOrDefault(f => f.Id == id && f.OrigenPedido == SD.OrigenPedidoDelivery);

        if (pedido == null) return FailResponse("Pedido delivery no encontrado.", StatusCodes.Status404NotFound);
        if (pedido.Estado == SD.EstadoOrdenPagado || pedido.Estado == SD.EstadoOrdenCancelado)
            return FailResponse("No se puede enviar a cocina un pedido pagado o cancelado.", StatusCodes.Status409Conflict);
        if (!pedido.FacturaServicios.Any())
            return FailResponse("No se puede enviar a cocina un pedido sin items.", StatusCodes.Status400BadRequest);
        if (!CocinaCatalogoHelper.OrdenTieneLineasCocina(pedido))
            return FailResponse("No hay productos que requieran cocina en este pedido.", StatusCodes.Status400BadRequest);
        if (pedido.EstadoCocina == SD.EstadoCocinaListo || pedido.EstadoCocina == SD.EstadoCocinaEntregado)
            return FailResponse("La cocina ya marcó este pedido como listo o entregado.", StatusCodes.Status409Conflict);

        var urlTicket = PublicRequestUrls.ImpresionCocinaAbsolute(Request, _configuration, pedido.Id);

        if (pedido.Estado == SD.EstadoOrdenEnCocina && pedido.EstadoCocina == SD.EstadoCocinaEnPreparacion)
        {
            return OkResponse(new
            {
                id = pedido.Id,
                estado = pedido.Estado,
                estadoCocina = pedido.EstadoCocina,
                urlImpresionCocina = urlTicket
            }, "Pedido ya estaba en cocina. Puede reimprimir el ticket.");
        }

        var ahora = DateTime.Now;
        pedido.Estado = SD.EstadoOrdenEnCocina;
        pedido.EstadoCocina = SD.EstadoCocinaEnPreparacion;
        pedido.FechaEnvioCocina = ahora;
        pedido.FechaActualizacion = ahora;

        foreach (var linea in CocinaCatalogoHelper.LineasCocina(pedido.FacturaServicios))
            linea.Estado = SD.EstadoCocinaEnPreparacion;

        _context.SaveChanges();

        return OkResponse(new
        {
            id = pedido.Id,
            estado = pedido.Estado,
            estadoCocina = pedido.EstadoCocina,
            pedido.FechaEnvioCocina,
            urlImpresionCocina = urlTicket
        }, "Pedido enviado a cocina");
    }

    [HttpGet("pedidos/{id:int}/precuenta")]
    public IActionResult Precuenta(int id)
    {
        var pedido = _context.Facturas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(f => f.FacturaServicios).ThenInclude(i => i.Servicio)
            .Include(f => f.FacturaServicios).ThenInclude(i => i.OpcionesSeleccionadas)
            .FirstOrDefault(f => f.Id == id && f.OrigenPedido == SD.OrigenPedidoDelivery);

        if (pedido == null) return FailResponse("Pedido delivery no encontrado.", StatusCodes.Status404NotFound);
        if (pedido.Estado == SD.EstadoOrdenCancelado) return FailResponse("No se puede generar pre-cuenta de un pedido cancelado.");

        var html = _impresionService.GenerarTicketComanda(pedido);
        return OkResponse(new
        {
            pedidoId = pedido.Id,
            pedidoNumero = pedido.Numero,
            urlImpresionPrecuenta = $"/api/v1/delivery/pedidos/{pedido.Id}/precuenta/html",
            htmlPrecuenta = html
        }, "Pre-cuenta delivery generada");
    }

    [HttpPost("pedidos/{id:int}/gestionar-pago")]
    public IActionResult GestionarPagoDelivery(int id, [FromBody] GestionarPagoVentaRequest request)
    {
        request.OrdenId = id;
        return EjecutarPagoDelivery(request, "Pago delivery gestionado");
    }

    [HttpPost("pedidos/{id:int}/procesar-pago")]
    public IActionResult ProcesarPagoDelivery(int id, [FromBody] ProcesarPagoVentaRequest request)
    {
        request.OrdenId = id;
        return EjecutarPagoDelivery(request, "Pago delivery procesado");
    }

    [HttpGet("pedidos/{id:int}/precuenta/html")]
    public IActionResult PrecuentaHtml(int id)
    {
        var pedido = _context.Facturas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(f => f.FacturaServicios).ThenInclude(i => i.Servicio)
            .Include(f => f.FacturaServicios).ThenInclude(i => i.OpcionesSeleccionadas)
            .FirstOrDefault(f => f.Id == id && f.OrigenPedido == SD.OrigenPedidoDelivery);

        if (pedido == null) return NotFound("Pedido delivery no encontrado.");
        if (pedido.Estado == SD.EstadoOrdenCancelado) return BadRequest("No se puede generar pre-cuenta de un pedido cancelado.");

        return Content(_impresionService.GenerarTicketComanda(pedido), "text/html");
    }

    /// <summary>Preferir <c>POST /api/v1/pedidos/{id}/cancelar</c> unificado. Misma validación de PIN e inventario.</summary>
    [HttpPost("pedidos/{id:int}/cancelar")]
    public IActionResult Cancelar(int id, [FromBody] CancelarPedidoRequest? request)
    {
        var pedidoCheck = _context.Facturas.AsNoTracking()
            .FirstOrDefault(f => f.Id == id && f.OrigenPedido == SD.OrigenPedidoDelivery);
        if (pedidoCheck == null)
            return FailResponse("Pedido delivery no encontrado.", StatusCodes.Status404NotFound);

        var pin = _configuracionService.ObtenerValor(SD.ConfigClavePinCancelacionPedidos)?.Trim();
        if (string.IsNullOrEmpty(pin))
            return FailResponse("Configure el PIN de cancelación en Configuraciones (PinCancelacionPedidos).", StatusCodes.Status503ServiceUnavailable);

        var codigo = request?.Codigo?.Trim();
        if (string.IsNullOrEmpty(codigo))
            return FailResponse("El código de verificación es requerido.", StatusCodes.Status400BadRequest);

        if (!string.Equals(codigo, pin, StringComparison.Ordinal))
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
            {
                Success = false,
                Message = "Código de verificación inválido.",
                Data = null
            });

        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue)
            return FailResponse("Usuario no autenticado.", StatusCodes.Status401Unauthorized);

        var err = _pedidoCancelacionService.EjecutarCancelacion(id, userId.Value);
        if (err != null)
            return FailResponse(err, err.Contains("no encontrado") ? StatusCodes.Status404NotFound : StatusCodes.Status409Conflict);

        return OkResponse(new { id, estado = SD.EstadoOrdenCancelado }, "Pedido delivery cancelado correctamente.");
    }

    /// <summary>
    /// Genera enlace wa.me para pedidos delivery usando plantilla de WhatsApp (id o default).
    /// </summary>
    [HttpPost("pedidos/{id:int}/whatsapp-link")]
    public IActionResult GenerarWhatsAppLink(int id, [FromBody] GenerarWhatsAppLinkRequest? request)
    {
        var pedido = _context.Facturas
            .AsNoTracking()
            .Where(f => f.Id == id && f.OrigenPedido == SD.OrigenPedidoDelivery)
            .FirstOrDefault();

        if (pedido == null)
            return FailResponse("Pedido delivery no encontrado.", StatusCodes.Status404NotFound);

        if (string.IsNullOrWhiteSpace(pedido.DeliveryClienteTelefono))
            return FailResponse("El pedido no tiene teléfono de cliente.", StatusCodes.Status409Conflict);

        var telefonoFormateado = _whatsAppService.FormatearNumeroWhatsApp(pedido.DeliveryClienteTelefono);
        if (!_whatsAppService.EsNumeroValidoParaWhatsApp(telefonoFormateado))
            return FailResponse("Teléfono inválido para WhatsApp (formato esperado: 505XXXXXXXX).", StatusCodes.Status400BadRequest);

        PlantillaMensajeWhatsApp? plantilla;
        if (request?.PlantillaId is int plantillaId)
        {
            plantilla = _whatsAppService.ObtenerPlantilla(plantillaId);
            if (plantilla == null)
                return FailResponse("Plantilla WhatsApp no encontrada o inactiva.", StatusCodes.Status404NotFound);
        }
        else
        {
            plantilla = _whatsAppService.ObtenerPlantillaDefault();
            if (plantilla == null)
                return FailResponse("No existe plantilla WhatsApp por defecto activa.", StatusCodes.Status500InternalServerError);
        }

        var clienteNombre = string.IsNullOrWhiteSpace(pedido.DeliveryClienteNombre)
            ? "cliente"
            : pedido.DeliveryClienteNombre.Trim();
        var fecha = pedido.FechaCreacion.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        var totalCordobas = $"C$ {pedido.Monto:N2}";

        var tokenPdf = PdfTokenHelper.GenerarTokenTemporal(pedido.Id, HttpContext.RequestServices.GetRequiredService<IConfiguration>());
        var enlacePdf = $"{Request.Scheme}://{Request.Host}/api/v1/public/facturas/{pedido.Id}/pdf?token={tokenPdf}";

        try
        {
            var mensaje = plantilla.Mensaje;
            mensaje = mensaje.Replace("{NombreCliente}", clienteNombre, StringComparison.Ordinal);
            mensaje = mensaje.Replace("{CodigoPedido}", pedido.Numero, StringComparison.Ordinal);
            mensaje = mensaje.Replace("{TotalCordobas}", totalCordobas, StringComparison.Ordinal);
            mensaje = mensaje.Replace("{Fecha}", fecha, StringComparison.Ordinal);
            mensaje = mensaje.Replace("{EnlacePDF}", enlacePdf, StringComparison.Ordinal);

            if (mensaje.Contains("{NombreCliente}", StringComparison.Ordinal)
                || mensaje.Contains("{CodigoPedido}", StringComparison.Ordinal)
                || mensaje.Contains("{TotalCordobas}", StringComparison.Ordinal)
                || mensaje.Contains("{Fecha}", StringComparison.Ordinal)
                || mensaje.Contains("{EnlacePDF}", StringComparison.Ordinal))
            {
                return FailResponse("Faltan datos para resolver placeholders de la plantilla.", StatusCodes.Status400BadRequest);
            }

            var waLink = _whatsAppService.GenerarEnlaceWhatsApp(telefonoFormateado, mensaje);
            if (string.IsNullOrWhiteSpace(waLink))
                return FailResponse("No se pudo construir el enlace de WhatsApp.", StatusCodes.Status500InternalServerError);

            var userId = SecurityHelper.GetUserId(User);
            _logger.LogInformation(
                "WhatsAppLink generado | PedidoId: {PedidoId} | Telefono: {Telefono} | PlantillaId: {PlantillaId} | Fecha: {Fecha} | UsuarioId: {UsuarioId}",
                pedido.Id,
                telefonoFormateado,
                plantilla.Id,
                DateTime.Now,
                userId);

            return OkResponse(new
            {
                pedidoId = pedido.Id,
                telefono = telefonoFormateado,
                plantillaId = plantilla.Id,
                mensaje,
                waLink,
                cliente = clienteNombre,
                codigoPedido = pedido.Numero
            }, "Enlace de WhatsApp generado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al construir enlace de WhatsApp para pedido {PedidoId}", pedido.Id);
            return FailResponse("Error al construir link de WhatsApp.", StatusCodes.Status500InternalServerError);
        }
    }

    private IActionResult EjecutarPagoDelivery(ProcesarPagoVentaRequest request, string mensajeExito)
    {
        if (request.OrdenId <= 0) return FailResponse("Pedido delivery inválido.");
        if (string.IsNullOrWhiteSpace(request.TipoPago)) return FailResponse("Tipo de pago es requerido.");

        var descuento = request.DescuentoMonto ?? 0m;
        descuento = Math.Round(descuento, 2, MidpointRounding.AwayFromZero);
        if (descuento < 0)
            return FailResponse("El descuento no puede ser negativo.", StatusCodes.Status400BadRequest);

        var cierre = _context.CierresCaja
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefault(c => c.Estado == "Abierto");
        if (cierre == null) return FailResponse("La caja está cerrada. Un administrador debe abrir la caja primero.", StatusCodes.Status409Conflict);

        var pedido = _context.Facturas
            .Include(f => f.FacturaServicios)
            .FirstOrDefault(f => f.Id == request.OrdenId && f.OrigenPedido == SD.OrigenPedidoDelivery);
        if (pedido == null) return FailResponse("Pedido delivery no encontrado.", StatusCodes.Status404NotFound);
        if (pedido.Estado == SD.EstadoOrdenPagado) return FailResponse("El pedido delivery ya fue pagado.");
        if (pedido.Estado == SD.EstadoOrdenCancelado) return FailResponse("No se puede cobrar un pedido delivery cancelado.", StatusCodes.Status409Conflict);
        if (!pedido.FacturaServicios.Any())
            return FailResponse("No se puede procesar/cobrar un pedido sin items.", StatusCodes.Status400BadRequest);

        var subtotalPedido = Math.Round(pedido.Monto, 2, MidpointRounding.AwayFromZero);
        if (subtotalPedido <= 0)
            return FailResponse("No se puede procesar/cobrar un pedido con total menor o igual a 0.", StatusCodes.Status400BadRequest);
        if (descuento > subtotalPedido + ToleranciaMonto)
            return FailResponse(
                $"El descuento (C$ {descuento:N2}) no puede superar el total del pedido (C$ {subtotalPedido:N2}).",
                StatusCodes.Status400BadRequest);

        var totalNetoCordobas = Math.Round(subtotalPedido - descuento, 2, MidpointRounding.AwayFromZero);
        if (totalNetoCordobas < 0)
            return FailResponse("El total neto después del descuento no puede ser negativo.", StatusCodes.Status400BadRequest);

        var moneda = string.IsNullOrWhiteSpace(request.Moneda) ? SD.MonedaCordoba : request.Moneda.Trim();
        var tipoCambio = decimal.TryParse(_context.Configuraciones.FirstOrDefault(c => c.Clave == "TipoCambioDolar")?.Valor, out var tc)
            ? tc
            : SD.TipoCambioDolar;

        decimal totalAValidar = totalNetoCordobas;
        if (moneda == SD.MonedaDolar)
            totalAValidar = tipoCambio <= 0 ? totalNetoCordobas : Math.Round(totalNetoCordobas / tipoCambio, 2, MidpointRounding.AwayFromZero);

        if (request.MontoPagado + ToleranciaMonto < totalAValidar)
            return FailResponse(
                $"Monto insuficiente. Total neto a pagar: {(moneda == SD.MonedaDolar ? $"${totalAValidar:N2} USD (≈ C$ {totalNetoCordobas:N2})" : $"C$ {totalNetoCordobas:N2}")}.",
                StatusCodes.Status400BadRequest);

        decimal montoPagadoCordobas = moneda == SD.MonedaDolar
            ? Math.Round(request.MontoPagado * tipoCambio, 2, MidpointRounding.AwayFromZero)
            : request.MontoPagado;
        decimal vueltoCordobas = moneda == SD.MonedaDolar
            ? Math.Round((request.MontoPagado - totalAValidar) * tipoCambio, 2, MidpointRounding.AwayFromZero)
            : Math.Round(request.MontoPagado - totalNetoCordobas, 2, MidpointRounding.AwayFromZero);

        var observaciones = ConstruirObservacionesPago(request.Observaciones, descuento, request.DescuentoMotivo);

        var pago = new Pago
        {
            FacturaId = pedido.Id,
            Monto = totalNetoCordobas,
            DescuentoMonto = descuento,
            DescuentoMotivo = string.IsNullOrWhiteSpace(request.DescuentoMotivo) ? null : request.DescuentoMotivo.Trim(),
            Moneda = moneda,
            TipoPago = request.TipoPago.Trim(),
            Banco = request.Banco,
            TipoCuenta = request.TipoCuenta,
            TipoCambio = tipoCambio,
            MontoRecibido = request.MontoPagado,
            Vuelto = vueltoCordobas < 0 ? 0 : vueltoCordobas,
            FechaPago = DateTime.Now,
            Observaciones = observaciones
        };

        if (pago.TipoPago == "Efectivo")
        {
            if (moneda == SD.MonedaDolar) pago.MontoDolaresFisico = request.MontoPagado;
            else pago.MontoCordobasFisico = request.MontoPagado;
        }
        else if (pago.TipoPago == "Mixto")
        {
            pago.MontoCordobasFisico = request.MontoCordobasFisico;
            pago.MontoDolaresFisico = request.MontoDolaresFisico;
            pago.MontoCordobasElectronico = request.MontoCordobasElectronico;
            pago.MontoDolaresElectronico = request.MontoDolaresElectronico;
        }
        else
        {
            if (moneda == SD.MonedaDolar) pago.MontoDolaresElectronico = request.MontoPagado;
            else pago.MontoCordobasElectronico = request.MontoPagado;
        }

        _context.Pagos.Add(pago);
        pedido.Estado = SD.EstadoOrdenPagado;
        pedido.FechaPagado = DateTime.Now;
        pedido.FechaActualizacion = DateTime.Now;
        _context.SaveChanges();

        return OkResponse(new
        {
            pagoId = pago.Id,
            pedidoId = pedido.Id,
            pedidoNumero = pedido.Numero,
            estado = pedido.Estado,
            vuelto = pago.Vuelto,
            urlImpresionRecibo = $"/api/v1/impresion/recibo/{pago.Id}",
            subtotalPedidoCordobas = subtotalPedido,
            descuentoCordobas = descuento,
            totalNetoCordobas = totalNetoCordobas,
            totalCordobas = totalNetoCordobas,
            montoPagadoCordobas = montoPagadoCordobas,
            vueltoCordobas = pago.Vuelto,
            tipoCambioAplicado = tipoCambio
        }, mensajeExito);
    }

    private static string? ConstruirObservacionesPago(string? observacionesCliente, decimal descuento, string? motivo)
    {
        var partes = new List<string>();
        if (descuento > 0)
        {
            var linea = $"Descuento C$ {descuento:N2}";
            if (!string.IsNullOrWhiteSpace(motivo))
                linea += $" - {motivo.Trim()}";
            partes.Add(linea);
        }
        if (!string.IsNullOrWhiteSpace(observacionesCliente))
            partes.Add(observacionesCliente.Trim());
        return partes.Count == 0 ? null : string.Join(" | ", partes);
    }

    private object MapPedidoDetalle(Factura pedido, List<Pago> pagos)
    {
        var subtotalPedidoCordobas = pedido.Monto;
        var descuentoCordobas = pagos.Sum(p => CobroFacturaHelper.DescuentoAtribuidoAPedido(p, pedido.Id));
        descuentoCordobas = Math.Round(descuentoCordobas, 2, MidpointRounding.AwayFromZero);
        var totalPagadoNetoCordobas = pagos.Sum(p => CobroFacturaHelper.NetoAplicadoAPedido(p, pedido.Id));
        totalPagadoNetoCordobas = Math.Round(totalPagadoNetoCordobas, 2, MidpointRounding.AwayFromZero);

        return new
        {
            id = pedido.Id,
            numero = pedido.Numero,
            codigo = pedido.Numero,
            origen = pedido.OrigenPedido == SD.OrigenPedidoDelivery ? "Delivery" : "Mesa",
            origenPedido = pedido.OrigenPedido,
            meseroId = pedido.MeseroId,
            mesero = pedido.Mesero != null ? pedido.Mesero.NombreCompleto : null,
            estado = pedido.Estado,
            estadoCocina = pedido.EstadoCocina,
            clienteNombre = pedido.DeliveryClienteNombre,
            clienteTelefono = pedido.DeliveryClienteTelefono,
            clienteDireccion = pedido.DeliveryClienteDireccion,
            observaciones = pedido.Observaciones,
            subtotal = subtotalPedidoCordobas,
            descuento = descuentoCordobas,
            recargoDelivery = 0m,
            impuestos = 0m,
            total = subtotalPedidoCordobas,
            totalPagadoNeto = totalPagadoNetoCordobas,
            createdAt = pedido.FechaCreacion,
            listoAt = pedido.FechaListo,
            paidAt = pedido.FechaPagado,
            cancelledAt = pedido.Estado == SD.EstadoOrdenCancelado ? pedido.FechaActualizacion : null,
            updatedAt = pedido.FechaActualizacion,
            pagos = pagos.Select(p => new
            {
                fecha = p.FechaPago,
                metodo = p.TipoPago,
                montoNeto = Math.Round(CobroFacturaHelper.NetoAplicadoAPedido(p, pedido.Id), 2, MidpointRounding.AwayFromZero),
                descuentoAtribuido = Math.Round(CobroFacturaHelper.DescuentoAtribuidoAPedido(p, pedido.Id), 2, MidpointRounding.AwayFromZero)
            }),
            items = pedido.FacturaServicios.Select(i => new
            {
                id = i.Id,
                productoId = i.ServicioId,
                servicioId = i.ServicioId,
                producto = i.Servicio.Nombre,
                cantidad = i.Cantidad,
                precioUnitario = i.PrecioUnitario,
                subtotal = i.Monto,
                estado = i.Estado,
                notas = i.Notas,
                opcionesResumen = ProductoOpcionesLineaHelper.OpcionesResumen(i.OpcionesSeleccionadas),
                opcionesSeleccionadas = ProductoOpcionesLineaHelper.MapOpcionesLineaRespuesta(i.OpcionesSeleccionadas)
            })
        };
    }

    private string GenerarNumeroDelivery()
    {
        var fecha = DateTime.Today;
        var ultimo = _context.Facturas
            .Where(f => f.OrigenPedido == SD.OrigenPedidoDelivery && f.FechaCreacion.Date == fecha)
            .OrderByDescending(f => f.Id)
            .Select(f => f.Numero)
            .FirstOrDefault();

        var consecutivo = 1;
        if (!string.IsNullOrWhiteSpace(ultimo))
        {
            var partes = ultimo.Split('-');
            if (partes.Length > 1 && int.TryParse(partes[^1], out var n))
                consecutivo = n + 1;
        }
        return $"D-{consecutivo:00000}";
    }
}

public class DeliveryGuardarPedidoRequest
{
    public int? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public string? ClienteTelefono { get; set; }
    public string? ClienteDireccion { get; set; }
    public string? Observaciones { get; set; }
    public List<ActualizarPedidoItemRequest> Items { get; set; } = new();
}

public class GenerarWhatsAppLinkRequest
{
    public int? PlantillaId { get; set; }
}
