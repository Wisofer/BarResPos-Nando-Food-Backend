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

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/pedidos")]
public class PedidosApiController : BaseApiController
{
    private readonly ApplicationDbContext _context;
    private readonly ExcelExportService _excelExportService;
    private readonly IImpresionService _impresionService;
    private readonly IInventarioService _inventarioService;
    private readonly OrdenLineasReemplazoService _lineasService;
    private readonly PedidoCancelacionService _pedidoCancelacionService;
    private readonly IConfiguracionService _configuracionService;
    private readonly ILogger<PedidosApiController> _logger;
    private readonly IConfiguration _configuration;

    public PedidosApiController(
        ApplicationDbContext context,
        ExcelExportService excelExportService,
        IImpresionService impresionService,
        IInventarioService inventarioService,
        OrdenLineasReemplazoService lineasService,
        PedidoCancelacionService pedidoCancelacionService,
        IConfiguracionService configuracionService,
        ILogger<PedidosApiController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _excelExportService = excelExportService;
        _impresionService = impresionService;
        _inventarioService = inventarioService;
        _lineasService = lineasService;
        _pedidoCancelacionService = pedidoCancelacionService;
        _configuracionService = configuracionService;
        _logger = logger;
        _configuration = configuration;
    }

    private static IQueryable<Factura> FiltrarPorTipoPedido(IQueryable<Factura> q, string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return q;
        return tipo.Trim().ToLowerInvariant() switch
        {
            "delivery" => q.Where(f => f.OrigenPedido == SD.OrigenPedidoDelivery),
            "llevar" => q.Where(f => f.OrigenPedido == SD.OrigenPedidoLlevar),
            "mesa" => q.Where(f =>
                f.OrigenPedido == SD.OrigenPedidoSalon
                || f.OrigenPedido == null
                || f.OrigenPedido == ""),
            _ => q
        };
    }

    /// <summary>
    /// Tras mover un pedido activo de mesa A a B: B → Ocupada; A → Libre si no quedan otros pedidos activos en A.
    /// </summary>
    private void SincronizarEstadosMesasPorPedido(Factura pedido, int? mesaIdAlInicio)
    {
        if (pedido.Estado == SD.EstadoOrdenPagado || pedido.Estado == SD.EstadoOrdenCancelado)
            return;

        if (pedido.MesaId.HasValue)
        {
            var mesaNueva = _context.Mesas.FirstOrDefault(m => m.Id == pedido.MesaId.Value && m.Activo);
            if (mesaNueva != null)
                mesaNueva.Estado = SD.EstadoMesaOcupada;
        }

        if (mesaIdAlInicio.HasValue && mesaIdAlInicio != pedido.MesaId)
        {
            var otrosEnOrigen = _context.Facturas.Count(f =>
                f.MesaId == mesaIdAlInicio.Value
                && f.Id != pedido.Id
                && f.Estado != SD.EstadoOrdenPagado
                && f.Estado != SD.EstadoOrdenCancelado);

            if (otrosEnOrigen == 0)
            {
                var mesaOrigen = _context.Mesas.FirstOrDefault(m => m.Id == mesaIdAlInicio.Value);
                if (mesaOrigen != null)
                    mesaOrigen.Estado = SD.EstadoMesaLibre;
            }
        }
    }

    /// <summary>
    /// Permite que PUT /pedidos/{id} con items: [] deje la orden vacía sin PIN:
    /// - elimina líneas y opciones,
    /// - devuelve inventario reservado (si ControlarStock=true),
    /// - pone monto en 0,
    /// - desacopla la mesa para liberarla si aplica.
    /// </summary>
    private string? VaciarPedidoSinLineas(Factura pedido, int usuarioId, int? mesaIdAlInicio)
    {
        using var tx = _context.Database.BeginTransaction();
        try
        {
            var cantidadesPorProducto = pedido.FacturaServicios
                .GroupBy(fs => fs.ServicioId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));

            if (cantidadesPorProducto.Count > 0)
            {
                var servicios = _context.Servicios
                    .Where(s => cantidadesPorProducto.Keys.Contains(s.Id))
                    .ToDictionary(s => s.Id, s => s);

                foreach (var (productoId, cantidad) in cantidadesPorProducto)
                {
                    if (!servicios.TryGetValue(productoId, out var svc)) continue;
                    if (!svc.ControlarStock) continue;
                    if (cantidad <= 0) continue;

                    _inventarioService.RegistrarEntrada(
                        productoId,
                        cantidad,
                        null,
                        null,
                        null,
                        $"Devolución por vaciado de pedido {pedido.Numero}",
                        usuarioId);
                }
            }

            _context.FacturaServicios.RemoveRange(pedido.FacturaServicios);
            pedido.FacturaServicios.Clear();

            pedido.Monto = 0;
            pedido.Estado = SD.EstadoOrdenGuardado;
            pedido.EstadoCocina = SD.EstadoCocinaPendiente;
            pedido.MesaId = null;
            pedido.FechaActualizacion = DateTime.Now;

            SincronizarEstadosMesasPorPedido(pedido, mesaIdAlInicio);

            _context.SaveChanges();
            tx.Commit();
            return null;
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "Error al vaciar pedido {PedidoId} desde items vacíos", pedido.Id);
            return "Error al vaciar el pedido.";
        }
    }

    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] string? estado,
        [FromQuery] string? tipo,
        [FromQuery] int? mesaId,
        [FromQuery] int? meseroId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _context.Facturas
            .AsNoTracking()
            .Include(f => f.Mesa)
            .Include(f => f.Cliente)
            .Include(f => f.Mesero)
            .AsQueryable();

        query = FiltrarPorTipoPedido(query, tipo);

        if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(f => f.Estado == estado);
        if (mesaId.HasValue) query = query.Where(f => f.MesaId == mesaId.Value);
        if (meseroId.HasValue) query = query.Where(f => f.MeseroId == meseroId.Value);
        if (desde.HasValue) query = query.Where(f => f.FechaCreacion >= desde.Value);
        if (hasta.HasValue) query = query.Where(f => f.FechaCreacion <= hasta.Value);

        var total = query.Count();
        var items = query
            .OrderByDescending(f => f.FechaCreacion)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new
            {
                f.Id,
                f.Numero,
                f.OrigenPedido,
                f.MesaId,
                Mesa = f.Mesa != null ? f.Mesa.Numero : null,
                f.ClienteId,
                Cliente = f.Cliente != null ? f.Cliente.Nombre : null,
                f.MeseroId,
                Mesero = f.Mesero != null ? f.Mesero.NombreCompleto : null,
                f.Estado,
                f.EstadoCocina,
                f.Monto,
                f.FechaCreacion,
                f.FechaPagado
            })
            .ToList();

        var ids = items.Select(i => i.Id).ToList();
        var productosPorPedido = _context.FacturaServicios
            .AsNoTracking()
            .Where(x => ids.Contains(x.FacturaId))
            .GroupBy(x => x.FacturaId)
            .Select(g => new { PedidoId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.PedidoId, x => x.Count);

        var paidIds = items.Where(i => i.Estado == SD.EstadoOrdenPagado).Select(i => i.Id).ToList();
        var pagosPorPedido = new Dictionary<int, (decimal Neto, decimal Descuento)>();
        if (paidIds.Count > 0)
        {
            var idSet = paidIds.ToHashSet();
            var pagosBatch = _context.Pagos
                .AsNoTracking()
                .Include(p => p.PagoFacturas)
                .Where(p => (p.FacturaId.HasValue && idSet.Contains(p.FacturaId.Value))
                    || p.PagoFacturas.Any(pf => idSet.Contains(pf.FacturaId)))
                .ToList();
            var netoMap = CobroFacturaHelper.NetoCobradoPorFactura(idSet, pagosBatch);
            var descMap = CobroFacturaHelper.DescuentoPorFactura(idSet, pagosBatch);
            foreach (var pid in paidIds)
                pagosPorPedido[pid] = (netoMap.GetValueOrDefault(pid), descMap.GetValueOrDefault(pid));
        }

        var itemsConProductos = items.Select(i => new
        {
            i.Id,
            i.Numero,
            tipo = PedidoOrigenHelper.TipoDesdeOrigen(i.OrigenPedido),
            origenPedido = i.OrigenPedido,
            i.MesaId,
            i.Mesa,
            i.ClienteId,
            i.Cliente,
            i.MeseroId,
            i.Mesero,
            i.Estado,
            i.EstadoCocina,
            i.Monto,
            SubtotalPedidoCordobas = i.Monto,
            DescuentoCordobas = i.Estado == SD.EstadoOrdenPagado && pagosPorPedido.TryGetValue(i.Id, out var pd) ? pd.Descuento : (decimal?)null,
            TotalNetoCobradoCordobas = i.Estado == SD.EstadoOrdenPagado && pagosPorPedido.TryGetValue(i.Id, out var pn) ? pn.Neto : (decimal?)null,
            i.FechaCreacion,
            i.FechaPagado,
            ProductosCount = productosPorPedido.TryGetValue(i.Id, out var count) ? count : 0
        }).ToList();

        return OkResponse(new PagedResult<object>
        {
            Items = itemsConProductos.Cast<object>().ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    [HttpGet("resumen")]
    public IActionResult Resumen(
        [FromQuery] string? estado,
        [FromQuery] string? tipo,
        [FromQuery] int? mesaId,
        [FromQuery] int? meseroId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var query = _context.Facturas
            .AsNoTracking()
            .AsQueryable();

        query = FiltrarPorTipoPedido(query, tipo);

        if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(f => f.Estado == estado);
        if (mesaId.HasValue) query = query.Where(f => f.MesaId == mesaId.Value);
        if (meseroId.HasValue) query = query.Where(f => f.MeseroId == meseroId.Value);
        if (desde.HasValue) query = query.Where(f => f.FechaCreacion >= desde.Value);
        if (hasta.HasValue) query = query.Where(f => f.FechaCreacion <= hasta.Value);

        var totalPedidos = query.Count();
        var pedidosMesa = query.Count(f =>
            f.OrigenPedido == SD.OrigenPedidoSalon || f.OrigenPedido == null || f.OrigenPedido == "");
        var pedidosDelivery = query.Count(f => f.OrigenPedido == SD.OrigenPedidoDelivery);
        var pedidosLlevar = query.Count(f => f.OrigenPedido == SD.OrigenPedidoLlevar);
        var pagados = query.Count(f => f.Estado == SD.EstadoOrdenPagado);
        var pendientes = query.Count(f => f.Estado == SD.EstadoOrdenPendiente || f.Estado == SD.EstadoOrdenEnCocina || f.Estado == SD.EstadoOrdenListo || f.Estado == SD.EstadoOrdenServido);
        var montoTotalConsumo = query.Sum(f => (decimal?)f.Monto) ?? 0;

        var pagadosIds = query.Where(f => f.Estado == SD.EstadoOrdenPagado).Select(f => f.Id).ToList();
        decimal montoTotalCobradoNeto = 0;
        decimal descuentoTotalCordobas = 0;
        if (pagadosIds.Count > 0)
        {
            var idSet = pagadosIds.ToHashSet();
            var pagosBatch = _context.Pagos
                .AsNoTracking()
                .Include(p => p.PagoFacturas)
                .Where(p => (p.FacturaId.HasValue && idSet.Contains(p.FacturaId.Value))
                    || p.PagoFacturas.Any(pf => idSet.Contains(pf.FacturaId)))
                .ToList();
            montoTotalCobradoNeto = CobroFacturaHelper.SumNetoCobrado(idSet, pagosBatch);
            descuentoTotalCordobas = CobroFacturaHelper.DescuentoPorFactura(idSet, pagosBatch).Values.Sum();
            descuentoTotalCordobas = Math.Round(descuentoTotalCordobas, 2, MidpointRounding.AwayFromZero);
        }

        return OkResponse(new
        {
            TotalPedidos = totalPedidos,
            PedidosPorTipo = new
            {
                mesa = pedidosMesa,
                delivery = pedidosDelivery,
                llevar = pedidosLlevar
            },
            Pagados = pagados,
            Pendientes = pendientes,
            MontoTotal = montoTotalConsumo,
            MontoTotalConsumoCordobas = montoTotalConsumo,
            MontoTotalCobradoNetoCordobas = montoTotalCobradoNeto,
            DescuentoTotalCordobas = descuentoTotalCordobas
        });
    }

    /// <summary>Cancelar cualquier pedido (mesa, delivery, llevar) con PIN. Devolución de stock solo en productos no preparados.</summary>
    [HttpPost("{id:int}/cancelar")]
    public IActionResult Cancelar(int id, [FromBody] CancelarPedidoRequest? request)
    {
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

        return OkResponse(new { id, estado = SD.EstadoOrdenCancelado }, "Pedido cancelado correctamente.");
    }

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var pedido = _context.Facturas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(f => f.Mesa)
            .Include(f => f.Cliente)
            .Include(f => f.Mesero)
            .Include(f => f.FacturaServicios)
                .ThenInclude(x => x.Servicio)
            .Include(f => f.FacturaServicios)
                .ThenInclude(x => x.OpcionesSeleccionadas)
            .FirstOrDefault(f => f.Id == id);

        if (pedido == null) return FailResponse("Pedido no encontrado.", StatusCodes.Status404NotFound);

        var tipo = PedidoOrigenHelper.TipoDesdeOrigen(pedido.OrigenPedido);

        var pagos = _context.Pagos
            .AsNoTracking()
            .Include(p => p.PagoFacturas)
            .Where(p => p.FacturaId == id || p.PagoFacturas.Any(pf => pf.FacturaId == id))
            .OrderByDescending(p => p.FechaPago)
            .ToList();

        var subtotalPedidoCordobas = pedido.Monto;
        var descuentoCordobas = pagos.Sum(p => CobroFacturaHelper.DescuentoAtribuidoAPedido(p, id));
        descuentoCordobas = Math.Round(descuentoCordobas, 2, MidpointRounding.AwayFromZero);
        var totalNetoCobradoCordobas = pagos.Sum(p => CobroFacturaHelper.NetoAplicadoAPedido(p, id));
        totalNetoCobradoCordobas = Math.Round(totalNetoCobradoCordobas, 2, MidpointRounding.AwayFromZero);

        var pagosDto = pagos.Select(p => new
        {
            p.Id,
            p.FechaPago,
            p.TipoPago,
            p.Moneda,
            MontoNetoCobradoCordobas = Math.Round(CobroFacturaHelper.NetoAplicadoAPedido(p, id), 2, MidpointRounding.AwayFromZero),
            p.Monto,
            DescuentoMontoEnPago = p.DescuentoMonto,
            DescuentoAtribuidoCordobas = Math.Round(CobroFacturaHelper.DescuentoAtribuidoAPedido(p, id), 2, MidpointRounding.AwayFromZero),
            p.DescuentoMotivo,
            p.MontoRecibido,
            p.Vuelto,
            p.TipoCambio,
            p.Observaciones
        }).ToList();

        return OkResponse(new
        {
            pedido.Id,
            pedido.Numero,
            tipo,
            origenPedido = pedido.OrigenPedido,
            pedido.MesaId,
            Mesa = pedido.Mesa?.Numero,
            pedido.ClienteId,
            Cliente = pedido.Cliente?.Nombre,
            pedido.MeseroId,
            Mesero = pedido.Mesero?.NombreCompleto,
            pedido.Estado,
            pedido.EstadoCocina,
            pedido.Monto,
            clienteNombreDelivery = pedido.OrigenPedido == SD.OrigenPedidoDelivery ? pedido.DeliveryClienteNombre : null,
            clienteTelefonoDelivery = pedido.OrigenPedido == SD.OrigenPedidoDelivery ? pedido.DeliveryClienteTelefono : null,
            clienteDireccionDelivery = pedido.OrigenPedido == SD.OrigenPedidoDelivery ? pedido.DeliveryClienteDireccion : null,
            SubtotalPedidoCordobas = subtotalPedidoCordobas,
            DescuentoCordobas = descuentoCordobas,
            TotalNetoCobradoCordobas = totalNetoCobradoCordobas,
            Pagos = pagosDto,
            pedido.Observaciones,
            pedido.FechaCreacion,
            pedido.FechaPagado,
            Items = pedido.FacturaServicios.Select(i => new
            {
                i.Id,
                i.ServicioId,
                Servicio = i.Servicio.Nombre,
                i.Cantidad,
                i.PrecioUnitario,
                i.Monto,
                i.Estado,
                i.Notas,
                opcionesResumen = ProductoOpcionesLineaHelper.OpcionesResumen(i.OpcionesSeleccionadas),
                opcionesSeleccionadas = ProductoOpcionesLineaHelper.MapOpcionesLineaRespuesta(i.OpcionesSeleccionadas)
            })
        });
    }

    private string UrlImpresionCocinaAbsoluta(int ordenId) =>
        PublicRequestUrls.ImpresionCocinaAbsolute(Request, _configuration, ordenId);

    /// <summary>
    /// Salón / para llevar: envía a cocina (KDS), marca fechas, pone líneas de cocina en preparación y devuelve URL del ticket (misma regla que KDS: <see cref="CocinaCatalogoHelper"/>).
    /// Delivery: usar <c>PATCH /api/v1/delivery/pedidos/{id}/enviar-cocina</c>.
    /// </summary>
    [HttpPatch("{id:int}/enviar-cocina")]
    public IActionResult EnviarCocina(int id)
    {
        var pedido = _context.Facturas
            .Include(f => f.FacturaServicios)
            .ThenInclude(l => l.Servicio)
            .ThenInclude(s => s!.CategoriaProducto)
            .FirstOrDefault(f => f.Id == id);

        if (pedido == null)
            return FailResponse("Pedido no encontrado.", StatusCodes.Status404NotFound);

        if (pedido.OrigenPedido == SD.OrigenPedidoDelivery)
            return FailResponse("Para delivery use PATCH /api/v1/delivery/pedidos/{id}/enviar-cocina.", StatusCodes.Status400BadRequest);

        if (pedido.Estado == SD.EstadoOrdenPagado || pedido.Estado == SD.EstadoOrdenCancelado)
            return FailResponse("No se puede enviar a cocina un pedido pagado o cancelado.", StatusCodes.Status409Conflict);

        if (!pedido.FacturaServicios.Any())
            return FailResponse("No se puede enviar a cocina un pedido sin items.", StatusCodes.Status400BadRequest);

        if (!CocinaCatalogoHelper.OrdenTieneLineasCocina(pedido))
            return FailResponse("No hay productos que requieran cocina en este pedido.", StatusCodes.Status400BadRequest);

        if (pedido.EstadoCocina == SD.EstadoCocinaListo || pedido.EstadoCocina == SD.EstadoCocinaEntregado)
            return FailResponse("La cocina ya marcó este pedido como listo o entregado.", StatusCodes.Status409Conflict);

        var urlTicket = UrlImpresionCocinaAbsoluta(pedido.Id);

        if (pedido.Estado == SD.EstadoOrdenEnCocina && pedido.EstadoCocina == SD.EstadoCocinaEnPreparacion)
        {
            return OkResponse(new
            {
                pedido.Id,
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

        SincronizarEstadosMesasPorPedido(pedido, pedido.MesaId);

        _context.SaveChanges();

        return OkResponse(new
        {
            pedido.Id,
            estado = pedido.Estado,
            estadoCocina = pedido.EstadoCocina,
            pedido.FechaEnvioCocina,
            urlImpresionCocina = urlTicket
        }, "Pedido enviado a cocina");
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] ActualizarPedidoRequest request)
    {
        var pedido = _context.Facturas
            .Include(f => f.FacturaServicios)
            .FirstOrDefault(f => f.Id == id);
        if (pedido == null) return FailResponse("Pedido no encontrado.", StatusCodes.Status404NotFound);
        if (pedido.Estado == SD.EstadoOrdenPagado || pedido.Estado == SD.EstadoOrdenCancelado)
            return FailResponse("No se puede editar un pedido pagado o cancelado.");
        if (pedido.OrigenPedido == SD.OrigenPedidoDelivery)
            return FailResponse("Los pedidos delivery se editan con PUT /api/v1/delivery/pedidos/{id}.", StatusCodes.Status400BadRequest);

        var mesaIdAlInicio = pedido.MesaId;

        if (request.Items != null)
        {
            var userId = SecurityHelper.GetUserId(User);
            if (!userId.HasValue)
                return FailResponse("Usuario no autenticado.", StatusCodes.Status401Unauthorized);

            if (request.Items.Count == 0)
            {
                var errVaciado = VaciarPedidoSinLineas(pedido, userId.Value, mesaIdAlInicio);
                if (errVaciado != null)
                    return FailResponse(errVaciado, StatusCodes.Status500InternalServerError);

                return OkResponse(new
                {
                    pedido.Id,
                    pedido.Monto,
                    pedido.Estado,
                    pedido.EstadoCocina,
                    pedido.MesaId,
                    vacio = true
                }, "Pedido vaciado correctamente.");
            }

            var refPedido = string.IsNullOrWhiteSpace(pedido.Numero) ? $"#{pedido.Id}" : pedido.Numero;
            var errLineas = _lineasService.ReemplazarLineas(_context, _inventarioService, pedido, request.Items, userId.Value, refPedido);
            if (errLineas != null)
                return FailResponse(errLineas, StatusCodes.Status400BadRequest);

            if (request.MesaId.HasValue) pedido.MesaId = request.MesaId;
            if (request.ClienteId.HasValue) pedido.ClienteId = request.ClienteId;
            if (request.MeseroId.HasValue) pedido.MeseroId = request.MeseroId;
            if (!string.IsNullOrWhiteSpace(request.Observaciones)) pedido.Observaciones = request.Observaciones.Trim();
            if (!string.IsNullOrWhiteSpace(request.Estado))
            {
                pedido.Estado = request.Estado.Trim();
                if (pedido.Estado == SD.EstadoOrdenPagado) pedido.FechaPagado = DateTime.Now;
                if (pedido.Estado == SD.EstadoOrdenEnCocina) pedido.FechaEnvioCocina ??= DateTime.Now;
            }
            if (!string.IsNullOrWhiteSpace(request.EstadoCocina)) pedido.EstadoCocina = request.EstadoCocina.Trim();

            pedido.FechaActualizacion = DateTime.Now;

            SincronizarEstadosMesasPorPedido(pedido, mesaIdAlInicio);

            _context.SaveChanges();

            return OkResponse(new { pedido.Id, pedido.Monto, pedido.Estado }, "Pedido actualizado");
        }

        if (request.MesaId.HasValue) pedido.MesaId = request.MesaId;
        if (request.ClienteId.HasValue) pedido.ClienteId = request.ClienteId;
        if (request.MeseroId.HasValue) pedido.MeseroId = request.MeseroId;
        if (!string.IsNullOrWhiteSpace(request.Observaciones)) pedido.Observaciones = request.Observaciones.Trim();
        if (!string.IsNullOrWhiteSpace(request.Estado))
        {
            pedido.Estado = request.Estado.Trim();
            if (pedido.Estado == SD.EstadoOrdenPagado) pedido.FechaPagado = DateTime.Now;
            if (pedido.Estado == SD.EstadoOrdenEnCocina) pedido.FechaEnvioCocina ??= DateTime.Now;
        }
        if (!string.IsNullOrWhiteSpace(request.EstadoCocina)) pedido.EstadoCocina = request.EstadoCocina.Trim();

        SincronizarEstadosMesasPorPedido(pedido, mesaIdAlInicio);

        _context.SaveChanges();
        return OkResponse(new { pedido.Id, pedido.Monto, pedido.Estado }, "Pedido actualizado");
    }

    /// <summary>Quita una línea del pedido (mesa/salón) sin reemplazar todo el carrito.</summary>
    [HttpDelete("{pedidoId:int}/lineas/{lineaId:int}")]
    public IActionResult EliminarLinea(int pedidoId, int lineaId)
    {
        var pedido = _context.Facturas
            .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.OpcionesSeleccionadas)
            .FirstOrDefault(f => f.Id == pedidoId);
        if (pedido == null) return FailResponse("Pedido no encontrado.", StatusCodes.Status404NotFound);
        if (pedido.Estado == SD.EstadoOrdenPagado || pedido.Estado == SD.EstadoOrdenCancelado)
            return FailResponse("No se puede editar un pedido pagado o cancelado.");
        if (pedido.OrigenPedido == SD.OrigenPedidoDelivery)
            return FailResponse("Use DELETE /api/v1/delivery/pedidos/{id}/lineas/{lineaId} para delivery.", StatusCodes.Status400BadRequest);

        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue)
            return FailResponse("Usuario no autenticado.", StatusCodes.Status401Unauthorized);

        var mesaIdAlInicio = pedido.MesaId;
        var refPedido = string.IsNullOrWhiteSpace(pedido.Numero) ? $"#{pedido.Id}" : pedido.Numero;
        var (vacio, err) = _lineasService.EliminarLinea(_context, _inventarioService, pedido, lineaId, userId.Value, refPedido);
        if (err != null)
            return FailResponse(err, StatusCodes.Status400BadRequest);

        SincronizarEstadosMesasPorPedido(pedido, mesaIdAlInicio);
        _context.SaveChanges();

        return OkResponse(new
        {
            pedido.Id,
            pedido.Monto,
            pedido.Estado,
            pedido.MesaId,
            vacio
        }, vacio ? "Línea eliminada. Pedido vacío." : "Línea eliminada.");
    }

    /// <summary>
    /// Mueve un pedido activo a otra mesa (misma orden, mismas líneas). Actualiza estados de mesas origen/destino.
    /// </summary>
    [HttpPatch("{id:int}/mesa")]
    public IActionResult CambiarMesa(int id, [FromBody] CambiarMesaPedidoRequest request)
    {
        if (request.MesaId <= 0)
            return FailResponse("mesaId debe ser un id de mesa válido.");

        var pedido = _context.Facturas.FirstOrDefault(f => f.Id == id);
        if (pedido == null) return FailResponse("Pedido no encontrado.", StatusCodes.Status404NotFound);
        if (pedido.Estado == SD.EstadoOrdenPagado || pedido.Estado == SD.EstadoOrdenCancelado)
            return FailResponse("No se puede cambiar la mesa de un pedido pagado o cancelado.", StatusCodes.Status409Conflict);

        if (pedido.MesaId == request.MesaId)
        {
            var mesaActual = _context.Mesas.AsNoTracking().FirstOrDefault(m => m.Id == request.MesaId);
            return OkResponse(new
            {
                pedido.Id,
                pedido.Numero,
                pedido.MesaId,
                Mesa = mesaActual?.Numero
            }, "El pedido ya está en esa mesa.");
        }

        var mesaDestino = _context.Mesas.FirstOrDefault(m => m.Id == request.MesaId && m.Activo);
        if (mesaDestino == null)
            return FailResponse("Mesa destino no encontrada o inactiva.", StatusCodes.Status404NotFound);

        var otroPedidoActivo = _context.Facturas.Any(f =>
            f.MesaId == request.MesaId
            && f.Id != pedido.Id
            && f.Estado != SD.EstadoOrdenPagado
            && f.Estado != SD.EstadoOrdenCancelado);

        if (otroPedidoActivo)
            return FailResponse("La mesa destino ya tiene otro pedido activo.", StatusCodes.Status409Conflict);

        if (mesaDestino.Estado != SD.EstadoMesaLibre)
            return FailResponse(
                $"La mesa destino no está libre (estado actual: {mesaDestino.Estado}). Elija una mesa libre.",
                StatusCodes.Status409Conflict);

        var mesaIdAlInicio = pedido.MesaId;
        pedido.MesaId = request.MesaId;

        SincronizarEstadosMesasPorPedido(pedido, mesaIdAlInicio);
        _context.SaveChanges();

        return OkResponse(new
        {
            pedido.Id,
            pedido.Numero,
            pedido.MesaId,
            Mesa = mesaDestino.Numero,
            pedido.Monto,
            pedido.Estado
        }, "Mesa del pedido actualizada.");
    }

    [HttpPatch("{id:int}/estado")]
    public IActionResult CambiarEstado(int id, [FromBody] CambiarEstadoPedidoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Estado)) return FailResponse("Estado es requerido.");

        var pedido = _context.Facturas.FirstOrDefault(f => f.Id == id);
        if (pedido == null) return FailResponse("Pedido no encontrado.", StatusCodes.Status404NotFound);

        pedido.Estado = request.Estado.Trim();
        if (pedido.Estado == SD.EstadoOrdenPagado) pedido.FechaPagado = DateTime.Now;
        _context.SaveChanges();

        return OkResponse(new { pedido.Id, pedido.Estado }, "Estado actualizado");
    }

    [HttpGet("{id:int}/precuenta")]
    public IActionResult Precuenta(int id)
    {
        var pedido = _context.Facturas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(f => f.Mesa)
            .Include(f => f.Mesero)
            .Include(f => f.FacturaServicios)
                .ThenInclude(x => x.Servicio)
            .Include(f => f.FacturaServicios)
                .ThenInclude(x => x.OpcionesSeleccionadas)
            .FirstOrDefault(f => f.Id == id);

        if (pedido == null) return FailResponse("Pedido no encontrado.", StatusCodes.Status404NotFound);
        if (pedido.Estado == SD.EstadoOrdenCancelado) return FailResponse("No se puede generar pre-cuenta de un pedido cancelado.");

        var html = _impresionService.GenerarTicketComanda(pedido);
        return OkResponse(new
        {
            PedidoId = pedido.Id,
            PedidoNumero = pedido.Numero,
            UrlImpresionPrecuenta = $"/api/v1/pedidos/{pedido.Id}/precuenta/html",
            HtmlPrecuenta = html
        }, "Pre-cuenta generada");
    }

    [HttpGet("{id:int}/precuenta/html")]
    public IActionResult PrecuentaHtml(int id)
    {
        var pedido = _context.Facturas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(f => f.Mesa)
            .Include(f => f.Mesero)
            .Include(f => f.FacturaServicios)
                .ThenInclude(x => x.Servicio)
            .Include(f => f.FacturaServicios)
                .ThenInclude(x => x.OpcionesSeleccionadas)
            .FirstOrDefault(f => f.Id == id);

        if (pedido == null) return NotFound("Pedido no encontrado.");
        if (pedido.Estado == SD.EstadoOrdenCancelado) return BadRequest("No se puede generar pre-cuenta de un pedido cancelado.");

        var html = _impresionService.GenerarTicketComanda(pedido);
        return Content(html, "text/html");
    }

    [HttpGet("exportar-excel")]
    public IActionResult ExportarExcel(
        [FromQuery] string? estado,
        [FromQuery] string? tipo,
        [FromQuery] int? mesaId,
        [FromQuery] int? meseroId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var query = _context.Facturas
            .AsNoTracking()
            .Include(f => f.Mesa)
            .Include(f => f.Mesero)
            .AsQueryable();

        query = FiltrarPorTipoPedido(query, tipo);

        if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(f => f.Estado == estado);
        if (mesaId.HasValue) query = query.Where(f => f.MesaId == mesaId.Value);
        if (meseroId.HasValue) query = query.Where(f => f.MeseroId == meseroId.Value);
        if (desde.HasValue) query = query.Where(f => f.FechaCreacion >= desde.Value);
        if (hasta.HasValue) query = query.Where(f => f.FechaCreacion <= hasta.Value);

        var pedidosBase = query
            .OrderByDescending(f => f.FechaCreacion)
            .Select(f => new
            {
                f.Id,
                f.Numero,
                f.OrigenPedido,
                f.FechaCreacion,
                Mesa = f.Mesa != null ? $"Mesa {f.Mesa.Numero}" : "-",
                Mesero = f.Mesero != null ? f.Mesero.NombreCompleto : "-",
                f.Estado,
                f.Monto,
                f.FechaPagado
            })
            .ToList();

        var paidExportIds = pedidosBase.Where(p => p.Estado == SD.EstadoOrdenPagado).Select(p => p.Id).ToList();
        var pagosExport = new Dictionary<int, (decimal Neto, decimal Desc)>();
        if (paidExportIds.Count > 0)
        {
            var idSet = paidExportIds.ToHashSet();
            var pagosBatch = _context.Pagos
                .AsNoTracking()
                .Include(p => p.PagoFacturas)
                .Where(p => (p.FacturaId.HasValue && idSet.Contains(p.FacturaId.Value))
                    || p.PagoFacturas.Any(pf => idSet.Contains(pf.FacturaId)))
                .ToList();
            var nm = CobroFacturaHelper.NetoCobradoPorFactura(idSet, pagosBatch);
            var dm = CobroFacturaHelper.DescuentoPorFactura(idSet, pagosBatch);
            foreach (var pid in paidExportIds)
                pagosExport[pid] = (nm.GetValueOrDefault(pid), dm.GetValueOrDefault(pid));
        }

        var pedidos = pedidosBase.Select(f => new
        {
            f.Numero,
            Tipo = PedidoOrigenHelper.TipoDesdeOrigen(f.OrigenPedido),
            f.FechaCreacion,
            f.Mesa,
            f.Mesero,
            f.Estado,
            Monto = f.Monto,
            DescuentoCordobas = f.Estado == SD.EstadoOrdenPagado && pagosExport.TryGetValue(f.Id, out var pe) ? pe.Desc : (decimal?)null,
            TotalNetoCobradoCordobas = f.Estado == SD.EstadoOrdenPagado && pagosExport.TryGetValue(f.Id, out var pn) ? pn.Neto : (decimal?)null,
            f.FechaPagado
        }).ToList();

        var excel = _excelExportService.ExportarPedidos(pedidos);
        var nombre = $"pedidos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }
}

public class CambiarEstadoPedidoRequest
{
    public string Estado { get; set; } = string.Empty;
}

public class CambiarMesaPedidoRequest
{
    public int MesaId { get; set; }
}
