using BarRestPOS.Data;
using BarRestPOS.Models.Api;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/pos")]
public class PosApiController : BaseApiController
{
    private readonly ApplicationDbContext _context;
    private readonly IInventarioService _inventarioService;
    private readonly PedidoCancelacionService _pedidoCancelacionService;
    private readonly IConfiguracionService _configuracionService;
    private readonly ILogger<PosApiController> _logger;

    public PosApiController(
        ApplicationDbContext context,
        IInventarioService inventarioService,
        PedidoCancelacionService pedidoCancelacionService,
        IConfiguracionService configuracionService,
        ILogger<PosApiController> logger)
    {
        _context = context;
        _inventarioService = inventarioService;
        _pedidoCancelacionService = pedidoCancelacionService;
        _configuracionService = configuracionService;
        _logger = logger;
    }

    [HttpPost("ordenes")]
    public IActionResult CrearOrden([FromBody] PosCrearOrdenRequest request)
    {
        if (request.Productos == null || request.Productos.Count == 0)
            return FailResponse("Debe agregar al menos un producto.");

        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) return FailResponse("Usuario no autenticado.", StatusCodes.Status401Unauthorized);

        var (abierta, cierreEvaluado) = ObtenerEstadoCajaActiva();
        _logger.LogInformation(
            "POS caja check -> usuarioId: {UsuarioId}, cierreId: {CierreId}, estadoCierre: {EstadoCierre}, cajaAbierta: {CajaAbierta}",
            userId.Value,
            cierreEvaluado?.Id,
            cierreEvaluado?.Estado,
            abierta
        );
        if (!abierta) return FailResponse("La caja está cerrada. Un administrador debe abrir la caja primero.", StatusCodes.Status409Conflict);

        Factura? orden = null;
        if (request.OrdenId.HasValue)
        {
            orden = _context.Facturas.Include(f => f.FacturaServicios).FirstOrDefault(f => f.Id == request.OrdenId.Value);
        }
        else if (request.MesaId.HasValue)
        {
            orden = _context.Facturas.Include(f => f.FacturaServicios)
                .Where(f => f.MesaId == request.MesaId && f.Estado != SD.EstadoOrdenPagado && f.Estado != SD.EstadoOrdenCancelado)
                .OrderByDescending(f => f.FechaCreacion)
                .FirstOrDefault();
        }

        if (orden == null)
        {
            var primerProducto = _context.Servicios.FirstOrDefault(s => s.Id == request.Productos[0].ProductoId && s.Activo);
            if (primerProducto == null) return FailResponse("Producto principal no encontrado.");

            var esLlevar = EsTipoLlevar(request.Tipo);
            orden = new Factura
            {
                Numero = GenerarNumeroOrden(),
                MesaId = request.MesaId,
                ClienteId = request.ClienteId,
                MeseroId = userId.Value,
                ServicioId = primerProducto.Id,
                Categoria = "General",
                OrigenPedido = esLlevar ? SD.OrigenPedidoLlevar : SD.OrigenPedidoSalon,
                Monto = 0,
                // MVP: nueva orden inicia en pendiente para mantener coherencia con "Enviar cocina"
                Estado = SD.EstadoOrdenPendiente,
                EstadoCocina = SD.EstadoCocinaPendiente,
                FechaCreacion = DateTime.Now,
                FechaListo = null,
                Observaciones = request.Observaciones
            };
            _context.Facturas.Add(orden);
            _context.SaveChanges();
        }

        var lineas = request.Productos.Where(p => p.Cantidad > 0).ToList();
        if (lineas.Count == 0)
            return FailResponse("Debe agregar al menos un producto con cantidad mayor a 0.");

        var cantidadPorProducto = lineas
            .GroupBy(p => p.ProductoId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));

        var idsLineas = lineas.Select(p => p.ProductoId).Distinct().ToList();
        var serviciosPorId = _context.Servicios
            .Include(s => s.OpcionGrupos)
            .ThenInclude(g => g.Opciones)
            .Where(s => idsLineas.Contains(s.Id) && s.Activo)
            .ToDictionary(s => s.Id);

        foreach (var (productoId, cantidadTotal) in cantidadPorProducto)
        {
            if (!serviciosPorId.TryGetValue(productoId, out var prod))
                return FailResponse($"Producto no encontrado o inactivo (id {productoId}).");
            if (prod.ControlarStock && !_inventarioService.ValidarStockDisponible(productoId, cantidadTotal))
                return FailResponse(
                    $"Stock insuficiente para {prod.Nombre}. Disponible: {prod.Stock}, solicitado en esta operación: {cantidadTotal}.",
                    StatusCodes.Status409Conflict);
        }

        decimal montoAgregado = 0;
        using var tx = _context.Database.BeginTransaction();
        try
        {
            foreach (var p in lineas)
            {
                var producto = serviciosPorId[p.ProductoId];
                var seleccionesDto = (p.OpcionesSeleccionadas ?? new List<OpcionSeleccionRequest>())
                    .Select(o => new OpcionSeleccionDto(o.GrupoId, o.OpcionId))
                    .ToList();
                var (adicional, filasOpc, errOp) = ProductoOpcionesLineaHelper.ValidarYConstruirFilas(producto, seleccionesDto);
                if (errOp != null)
                {
                    tx.Rollback();
                    return FailResponse(errOp, StatusCodes.Status400BadRequest);
                }

                var precioUnitario = Math.Round(producto.Precio + adicional, 2, MidpointRounding.AwayFromZero);
                var subtotal = Math.Round(precioUnitario * p.Cantidad, 2, MidpointRounding.AwayFromZero);
                montoAgregado += subtotal;

                if (producto.ControlarStock)
                {
                    try
                    {
                        _inventarioService.RegistrarSalida(
                            producto.Id,
                            p.Cantidad,
                            SD.SubtipoMovimientoVenta,
                            orden.Id,
                            $"Venta POS — orden {orden.Numero}",
                            userId.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al descontar stock producto {ProductoId} orden {OrdenId}", producto.Id, orden.Id);
                        tx.Rollback();
                        return FailResponse(
                            $"No se pudo reservar stock para {producto.Nombre}: {ex.Message}",
                            StatusCodes.Status409Conflict);
                    }
                }

                var linea = new FacturaServicio
                {
                    FacturaId = orden.Id,
                    ServicioId = producto.Id,
                    Cantidad = p.Cantidad,
                    PrecioUnitario = precioUnitario,
                    Monto = subtotal,
                    Notas = p.Notas,
                    Estado = SD.EstadoCocinaPendiente
                };
                foreach (var op in filasOpc)
                    linea.OpcionesSeleccionadas.Add(op);
                _context.FacturaServicios.Add(linea);
            }

            orden.Monto += montoAgregado;
            if (orden.MesaId.HasValue)
            {
                var mesa = _context.Mesas.FirstOrDefault(m => m.Id == orden.MesaId.Value);
                if (mesa != null) mesa.Estado = SD.EstadoMesaOcupada;
            }

            _context.SaveChanges();
            tx.Commit();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "Error al guardar orden POS {OrdenId}", orden.Id);
            return FailResponse("Error al guardar la orden. Reintente.", StatusCodes.Status500InternalServerError);
        }

        return OkResponse(new
        {
            orden.Id,
            orden.Numero,
            orden.Monto
        }, "Orden guardada");
    }

    [HttpPost("ordenes/{id:int}/cancelar")]
    public IActionResult CancelarOrden(int id, [FromBody] CancelarPedidoRequest? request)
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

        return OkResponse(new { id, estado = SD.EstadoOrdenCancelado }, "Orden cancelada correctamente.");
    }

    private (bool abierta, CierreCaja? cierre) ObtenerEstadoCajaActiva()
    {
        var cierre = _context.CierresCaja
            .AsNoTracking()
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefault(c => c.Estado == "Abierto");

        return (cierre != null, cierre);
    }

    private static bool EsTipoLlevar(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return false;
        var t = tipo.Trim().ToLowerInvariant();
        return t is "llevar" or "para llevar" or "para-llevar" or "takeout";
    }

    private string GenerarNumeroOrden()
    {
        var fecha = DateTime.Today;
        var ultimo = _context.Facturas
            .Where(f => f.FechaCreacion.Date == fecha)
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
        return $"ORD-{consecutivo:00000}";
    }
}

public class PosCrearOrdenRequest
{
    public int? OrdenId { get; set; }
    public int? MesaId { get; set; }
    public int? ClienteId { get; set; }
    /// <summary>mesa (default) o llevar — pedidos para llevar aparecen en listados con tipo llevar.</summary>
    public string? Tipo { get; set; }
    public string? Observaciones { get; set; }
    public List<PosOrdenItemRequest> Productos { get; set; } = new();
}

public class PosOrdenItemRequest
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public string? Notas { get; set; }

    /// <summary>Opciones elegidas por grupo (vacío si el producto no tiene grupos o no aplica).</summary>
    public List<OpcionSeleccionRequest>? OpcionesSeleccionadas { get; set; }
}
