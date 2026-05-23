using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class ReporteService : IReporteService
{
    private readonly ApplicationDbContext _context;
    private readonly ExcelExportService _excelExportService;

    public ReporteService(ApplicationDbContext context, ExcelExportService excelExportService)
    {
        _context = context;
        _excelExportService = excelExportService;
    }

    public async Task<ResumenVentasResponse> ObtenerResumenVentasAsync(DateTime? desde, DateTime? hasta)
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today);
        var facturas = await QueryFacturasPagadas(fDesde, fHasta).ToListAsync();
        var netoPorFactura = await GetNetoPorFacturaAsync(facturas);

        decimal NetoCobrado(IEnumerable<Factura> fs) =>
            Math.Round(fs.Sum(f => netoPorFactura.GetValueOrDefault(f.Id)), 2, MidpointRounding.AwayFromZero);

        var totalVentas = NetoCobrado(facturas);
        var totalOrdenes = facturas.Count;

        var porDia = facturas
            .GroupBy(f => f.FechaPagado!.Value.Date)
            .Select(g => new VentaPorDiaReporte
            {
                Fecha = g.Key,
                Total = NetoCobrado(g),
                Ordenes = g.Count()
            })
            .OrderBy(x => x.Fecha)
            .ToList();

        return new ResumenVentasResponse
        {
            Desde = fDesde,
            Hasta = fHasta,
            TotalVentas = totalVentas,
            TotalOrdenes = totalOrdenes,
            PromedioTicket = totalOrdenes > 0 ? (totalVentas / totalOrdenes) : 0,
            PorDia = porDia
        };
    }

    public async Task<List<VentaDetalleReporte>> ObtenerDetalleVentasAsync(DateTime? desde, DateTime? hasta, string? filtroVentas = "activas")
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today);
        var facturas = await QueryFacturasDetalleReporte(fDesde, fHasta, filtroVentas)
            .Include(f => f.Mesa)
            .Include(f => f.Cliente)
            .Include(f => f.Mesero)
            .Include(f => f.FacturaServicios)
            .OrderBy(f => f.FechaCreacion)
            .ThenBy(f => f.Id)
            .ToListAsync();

        var (netoPorFactura, pagosBatch) = await CargarNetoYPagosAsync(facturas);

        return facturas.Select(f =>
        {
            var fechaBase = f.FechaPagado ?? f.FechaActualizacion ?? f.FechaCreacion;
            var subtotalLineas = Math.Round(f.FacturaServicios.Sum(x => x.Monto), 2, MidpointRounding.AwayFromZero);
            var totalNeto = f.Estado == SD.EstadoOrdenPagado
                ? netoPorFactura.GetValueOrDefault(f.Id, subtotalLineas)
                : subtotalLineas;
            return new VentaDetalleReporte
            {
                Id = f.Id,
                Numero = f.Numero,
                Fecha = fechaBase,
                Origen = f.OrigenPedido == SD.OrigenPedidoDelivery ? "Delivery" : (f.OrigenPedido == SD.OrigenPedidoLlevar ? "Llevar" : "Mesa"),
                Referencia = f.OrigenPedido == SD.OrigenPedidoDelivery
                    ? (f.DeliveryClienteTelefono ?? f.DeliveryClienteNombre ?? "-")
                    : (f.Mesa != null ? $"Mesa {f.Mesa.Numero}" : "-"),
                Cliente = f.OrigenPedido == SD.OrigenPedidoDelivery ? (f.DeliveryClienteNombre ?? f.Cliente?.Nombre) : f.Cliente?.Nombre,
                Mesero = f.Mesero?.NombreCompleto,
                SubtotalLineas = subtotalLineas,
                CantidadLineas = f.FacturaServicios.Count,
                Total = totalNeto,
                Estado = f.Estado,
                FechaUltimaActualizacion = f.FechaActualizacion ?? f.FechaCreacion,
                MetodoPago = MetodoPagoDeFactura(f.Id, pagosBatch),
                Moneda = MonedaDeFactura(f.Id, pagosBatch)
            };
        }).ToList();
    }

    public async Task<VentaTicketCompletoReporte?> ObtenerTicketCompletoPorOrdenIdAsync(int ordenId)
    {
        var f = await _context.Facturas.AsNoTracking()
            .Include(x => x.Mesa)
            .Include(x => x.Cliente)
            .Include(x => x.Mesero)
            .Include(x => x.FacturaServicios).ThenInclude(d => d.Servicio)
            .FirstOrDefaultAsync(x => x.Id == ordenId);

        if (f == null) return null;
        if (f.Estado != SD.EstadoOrdenPagado && f.Estado != SD.EstadoOrdenCancelado) return null;

        var (netoMap, pagosBatch) = await CargarNetoYPagosAsync(new[] { f });
        var lineas = f.FacturaServicios.OrderBy(d => d.Id).Select(d => new VentaLineaReporte
        {
            DetalleId = d.Id,
            Anulado = f.Estado == SD.EstadoOrdenCancelado,
            ProductoId = d.ServicioId,
            CodigoProducto = d.Servicio?.Codigo ?? "",
            NombreProducto = d.Servicio?.Nombre ?? "",
            Cantidad = d.Cantidad,
            PrecioUnitario = d.PrecioUnitario,
            TotalLinea = d.Monto,
            Notas = d.Notas
        }).ToList();

        var unidades = lineas.Sum(l => l.Cantidad);
        var subLineas = Math.Round(lineas.Sum(l => l.TotalLinea), 2, MidpointRounding.AwayFromZero);

        return new VentaTicketCompletoReporte
        {
            Id = f.Id,
            Numero = f.Numero,
            Fecha = f.FechaPagado ?? f.FechaActualizacion ?? f.FechaCreacion,
            Cliente = f.OrigenPedido == SD.OrigenPedidoDelivery ? (f.DeliveryClienteNombre ?? f.Cliente?.Nombre) : f.Cliente?.Nombre,
            Mesero = f.Mesero?.NombreCompleto,
            Origen = f.OrigenPedido == SD.OrigenPedidoDelivery ? "Delivery" : (f.OrigenPedido == SD.OrigenPedidoLlevar ? "Llevar" : "Mesa"),
            Estado = f.Estado,
            SubtotalLineas = subLineas,
            TotalCobrado = netoMap.GetValueOrDefault(f.Id, subLineas),
            CantidadLineas = lineas.Count,
            CantidadUnidades = unidades,
            MetodoPago = MetodoPagoDeFactura(f.Id, pagosBatch),
            Moneda = MonedaDeFactura(f.Id, pagosBatch),
            Lineas = lineas
        };
    }

    public async Task<List<VentaPorCategoriaReporte>> ObtenerVentasPorCategoriaAsync(DateTime? desde, DateTime? hasta)
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today.AddDays(-30));
        var raw = await _context.FacturaServicios.AsNoTracking()
            .Include(d => d.Factura)
            .Include(d => d.Servicio).ThenInclude(s => s.CategoriaProducto)
            .Where(d => d.Factura.Estado == SD.EstadoOrdenPagado
                && d.Factura.FechaPagado.HasValue
                && d.Factura.FechaPagado.Value >= fDesde
                && d.Factura.FechaPagado.Value <= fHasta)
            .Select(d => new
            {
                Categoria = d.Servicio.CategoriaProducto != null ? d.Servicio.CategoriaProducto.Nombre : (d.Servicio.Categoria ?? "Sin categoría"),
                d.Monto,
                d.Cantidad
            })
            .ToListAsync();

        return raw.GroupBy(x => x.Categoria)
            .Select(g => new VentaPorCategoriaReporte
            {
                Categoria = g.Key,
                Monto = Math.Round(g.Sum(x => x.Monto), 2, MidpointRounding.AwayFromZero),
                Cantidad = g.Sum(x => x.Cantidad)
            })
            .OrderByDescending(x => x.Monto)
            .ToList();
    }

    public async Task<List<VentaPorCategoriaConDesgloseReporte>> ObtenerVentasPorCategoriaConDesgloseAsync(DateTime? desde, DateTime? hasta)
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today.AddDays(-30));
        var filas = await _context.FacturaServicios.AsNoTracking()
            .Include(d => d.Factura)
            .Include(d => d.Servicio).ThenInclude(s => s.CategoriaProducto)
            .Where(d => d.Factura.Estado == SD.EstadoOrdenPagado
                && d.Factura.FechaPagado.HasValue
                && d.Factura.FechaPagado.Value >= fDesde
                && d.Factura.FechaPagado.Value <= fHasta)
            .Select(d => new
            {
                Categoria = d.Servicio.CategoriaProducto != null ? d.Servicio.CategoriaProducto.Nombre : (d.Servicio.Categoria ?? "Sin categoría"),
                ProductoId = d.ServicioId,
                Codigo = d.Servicio.Codigo,
                Nombre = d.Servicio.Nombre,
                d.Cantidad,
                d.Monto
            })
            .ToListAsync();

        return filas.GroupBy(x => x.Categoria)
            .Select(cat =>
            {
                var productos = cat.GroupBy(x => x.ProductoId)
                    .Select(g =>
                    {
                        var first = g.First();
                        return new VentaPorCategoriaProductoDesglose
                        {
                            ProductoId = g.Key,
                            CodigoProducto = first.Codigo ?? "",
                            NombreProducto = first.Nombre ?? "",
                            Cantidad = g.Sum(x => x.Cantidad),
                            Monto = Math.Round(g.Sum(x => x.Monto), 2, MidpointRounding.AwayFromZero)
                        };
                    })
                    .OrderByDescending(p => p.Monto)
                    .ToList();
                return new VentaPorCategoriaConDesgloseReporte
                {
                    Categoria = cat.Key,
                    Monto = Math.Round(cat.Sum(x => x.Monto), 2, MidpointRounding.AwayFromZero),
                    Cantidad = cat.Sum(x => x.Cantidad),
                    Productos = productos
                };
            })
            .OrderByDescending(c => c.Monto)
            .ToList();
    }

    public async Task<List<ProductoTopReporte>> ObtenerProductosTopAsync(DateTime? desde, DateTime? hasta, int top)
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today.AddDays(-30));
        var limit = Math.Min(Math.Max(top, 1), 100);

        var lineas = await _context.FacturaServicios.AsNoTracking()
            .Include(i => i.Factura)
            .Include(i => i.Servicio).ThenInclude(s => s.CategoriaProducto)
            .Where(i => i.Factura.Estado == SD.EstadoOrdenPagado
                && i.Factura.FechaPagado.HasValue
                && i.Factura.FechaPagado.Value >= fDesde
                && i.Factura.FechaPagado.Value <= fHasta)
            .ToListAsync();

        if (lineas.Count == 0) return new List<ProductoTopReporte>();

        var facturaIds = lineas.Select(l => l.FacturaId).Distinct().ToHashSet();
        var pagosBatch = await _context.Pagos.AsNoTracking()
            .Include(p => p.PagoFacturas)
            .Where(p => (p.FacturaId.HasValue && facturaIds.Contains(p.FacturaId.Value))
                || p.PagoFacturas.Any(pf => facturaIds.Contains(pf.FacturaId)))
            .ToListAsync();

        var enriched = lineas.Select(l => (
            ProductoId: l.ServicioId,
            Categoria: l.Servicio?.CategoriaProducto?.Nombre ?? l.Servicio?.Categoria ?? "",
            Nombre: l.Servicio?.Nombre ?? "Producto eliminado",
            Cantidad: l.Cantidad,
            Total: l.Monto,
            Metodo: MetodoPagoDeFactura(l.FacturaId, pagosBatch),
            Moneda: MonedaDeFactura(l.FacturaId, pagosBatch)
        )).ToList();

        var topProductos = enriched.GroupBy(x => x.ProductoId)
            .Select(g => new
            {
                ProductoId = g.Key,
                Cantidad = g.Sum(x => x.Cantidad),
                Venta = g.Sum(x => x.Total),
                Categoria = g.First().Categoria,
                Producto = g.First().Nombre
            })
            .OrderByDescending(x => x.Cantidad)
            .Take(limit)
            .ToList();

        var result = new List<ProductoTopReporte>();
        foreach (var p in topProductos)
        {
            var filas = enriched.Where(x => x.ProductoId == p.ProductoId).ToList();
            var desglose = filas
                .GroupBy(x => (x.Metodo, x.Moneda))
                .Select(g => new ProductoTopDesglosePago
                {
                    MetodoPago = g.Key.Metodo,
                    Moneda = g.Key.Moneda,
                    CantidadUnidades = g.Sum(x => x.Cantidad),
                    MontoCordobas = Math.Round(g.Sum(x => x.Total), 2, MidpointRounding.AwayFromZero)
                })
                .OrderByDescending(x => x.MontoCordobas)
                .ToList();

            result.Add(new ProductoTopReporte
            {
                ProductoId = p.ProductoId,
                Categoria = p.Categoria,
                Producto = p.Producto,
                Cantidad = p.Cantidad,
                Venta = Math.Round(p.Venta, 2, MidpointRounding.AwayFromZero),
                DesglosePorFormaPago = desglose
            });
        }

        return result;
    }

    public async Task<List<VentaPorMeseroReporte>> ObtenerVentasPorMeseroAsync(DateTime? desde, DateTime? hasta)
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today.AddDays(-30));
        var facturas = await QueryFacturasPagadas(fDesde, fHasta).Include(f => f.Mesero).ToListAsync();
        var netoPorFactura = await GetNetoPorFacturaAsync(facturas);

        return facturas.GroupBy(f => new { f.MeseroId, Mesero = f.Mesero?.NombreCompleto ?? "Sin mesero" })
            .Select(g =>
            {
                var ordenes = g.Count();
                var totalNeto = Math.Round(g.Sum(x => netoPorFactura.GetValueOrDefault(x.Id)), 2, MidpointRounding.AwayFromZero);
                return new VentaPorMeseroReporte
                {
                    MeseroId = g.Key.MeseroId,
                    Mesero = g.Key.Mesero,
                    CantidadOrdenes = ordenes,
                    TotalNeto = totalNeto,
                    PromedioTicket = ordenes > 0 ? Math.Round(totalNeto / ordenes, 2, MidpointRounding.AwayFromZero) : 0m
                };
            })
            .OrderByDescending(x => x.TotalNeto)
            .ToList();
    }

    public byte[] GenerarExcelVentas(DateTime desde, DateTime hasta, List<VentaDetalleReporte> ventas)
    {
        var rows = ventas.Select(v => new
        {
            numero = v.Numero,
            fecha = v.Fecha,
            estado = v.Estado,
            origen = v.Origen,
            referencia = v.Referencia,
            metodoPago = v.MetodoPago,
            moneda = v.Moneda,
            lineas = v.CantidadLineas,
            subtotalLineas = v.SubtotalLineas,
            total = v.Total
        }).ToList();
        return _excelExportService.ExportarVentasReporte(rows);
    }

    public byte[] GenerarExcelCategorias(DateTime desde, DateTime hasta, List<VentaPorCategoriaReporte> items) =>
        _excelExportService.ExportarVentasPorCategoria(items.Select(x => new { Categoria = x.Categoria, Cantidad = x.Cantidad, Monto = x.Monto }).ToList());

    public byte[] GenerarExcelCategoriasConDesglose(DateTime desde, DateTime hasta, List<VentaPorCategoriaConDesgloseReporte> items) =>
        _excelExportService.ExportarVentasPorCategoriaConDesglose(items);

    public byte[] GenerarExcelTopProductos(DateTime desde, DateTime hasta, List<ProductoTopReporte> items) =>
        _excelExportService.ExportarTopProductos(items);

    public byte[] GenerarExcelVentasPorMesero(DateTime desde, DateTime hasta, List<VentaPorMeseroReporte> items) =>
        _excelExportService.ExportarVentasPorMesero(items.Select(x => new
        {
            mesero = x.Mesero,
            ordenes = x.CantidadOrdenes,
            totalVentas = x.TotalNeto,
            promedioTicket = x.PromedioTicket
        }));

    private static (DateTime desde, DateTime hasta) ResolverRango(DateTime? desde, DateTime? hasta, DateTime fallbackDesde)
    {
        var fDesde = desde?.Date ?? fallbackDesde.Date;
        var fHasta = (hasta?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);
        return (fDesde, fHasta);
    }

    private IQueryable<Factura> QueryFacturasPagadas(DateTime fDesde, DateTime fHasta) =>
        _context.Facturas.AsNoTracking()
            .Where(f => f.Estado == SD.EstadoOrdenPagado
                && f.FechaPagado.HasValue
                && f.FechaPagado.Value >= fDesde
                && f.FechaPagado.Value <= fHasta);

    private IQueryable<Factura> QueryFacturasDetalleReporte(DateTime fDesde, DateTime fHasta, string? filtroVentas)
    {
        var s = (filtroVentas ?? "activas").Trim().ToLowerInvariant();
        return s switch
        {
            "anuladas" => _context.Facturas.AsNoTracking().Where(f =>
                f.Estado == SD.EstadoOrdenCancelado
                && (f.FechaActualizacion ?? f.FechaCreacion) >= fDesde
                && (f.FechaActualizacion ?? f.FechaCreacion) <= fHasta),
            "todas" => _context.Facturas.AsNoTracking().Where(f =>
                ((f.Estado == SD.EstadoOrdenPagado && f.FechaPagado.HasValue && f.FechaPagado.Value >= fDesde && f.FechaPagado.Value <= fHasta)
                || (f.Estado == SD.EstadoOrdenCancelado && (f.FechaActualizacion ?? f.FechaCreacion) >= fDesde && (f.FechaActualizacion ?? f.FechaCreacion) <= fHasta))),
            _ => QueryFacturasPagadas(fDesde, fHasta)
        };
    }

    private async Task<Dictionary<int, decimal>> GetNetoPorFacturaAsync(IEnumerable<Factura> facturas)
    {
        var (neto, _) = await CargarNetoYPagosAsync(facturas);
        return neto;
    }

    private async Task<(Dictionary<int, decimal> Neto, List<Pago> Pagos)> CargarNetoYPagosAsync(IEnumerable<Factura> facturas)
    {
        var facturaIds = facturas.Select(f => f.Id).ToHashSet();
        if (facturaIds.Count == 0) return (new Dictionary<int, decimal>(), new List<Pago>());
        var pagosBatch = await _context.Pagos.AsNoTracking()
            .Include(p => p.PagoFacturas)
            .Where(p => (p.FacturaId.HasValue && facturaIds.Contains(p.FacturaId.Value))
                || p.PagoFacturas.Any(pf => facturaIds.Contains(pf.FacturaId)))
            .ToListAsync();
        var neto = CobroFacturaHelper.NetoCobradoPorFactura(facturaIds, pagosBatch);
        return (neto, pagosBatch);
    }

    private static string MetodoPagoDeFactura(int facturaId, List<Pago> pagos)
    {
        var tipos = pagos
            .Where(p => CobroFacturaHelper.NetoAplicadoAPedido(p, facturaId) > 0)
            .Select(p => p.TipoPago)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToList();
        if (tipos.Count == 0) return "";
        if (tipos.Count > 1) return "Mixto";
        return tipos[0];
    }

    private static string? MonedaDeFactura(int facturaId, List<Pago> pagos)
    {
        foreach (var p in pagos.OrderBy(x => x.FechaPago))
        {
            if (CobroFacturaHelper.NetoAplicadoAPedido(p, facturaId) > 0)
                return string.IsNullOrWhiteSpace(p.Moneda) ? null : p.Moneda.Trim();
        }
        return null;
    }
}
