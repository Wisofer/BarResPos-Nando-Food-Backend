using BarRestPOS.Data;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;

    public DashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<object> ObtenerResumenAsync(DateTime? desde, DateTime? hasta, int topProductos)
    {
        if (topProductos < 1) topProductos = 5;
        if (topProductos > 20) topProductos = 20;

        var hoy = DateTime.Today;
        var inicioRango = (desde?.Date ?? hoy.AddDays(-6));
        var finRango = (hasta?.Date ?? hoy).AddDays(1).AddTicks(-1);
        var inicioHoy = hoy;
        var finHoy = hoy.AddDays(1).AddTicks(-1);
        var inicioSemana = hoy.AddDays(-(int)hoy.DayOfWeek);
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

        var cajaHoy = await _context.CierresCaja
            .AsNoTracking()
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefaultAsync(c => c.Estado == "Abierto" || c.FechaCierre.Date == hoy);

        if (cajaHoy != null && cajaHoy.Estado == "Abierto")
        {
            inicioHoy = cajaHoy.FechaHoraCierre;
            finHoy = DateTime.Now;
        }

        var pagosHoy = await _context.Pagos
            .AsNoTracking()
            .Where(p => p.FechaPago >= inicioHoy && p.FechaPago <= finHoy)
            .ToListAsync();
        var totalCajaHoyNeto = Math.Round(pagosHoy.Sum(p => p.Monto), 2, MidpointRounding.AwayFromZero);

        var ordenesPendientesPago = await _context.Facturas
            .AsNoTracking()
            .CountAsync(f => f.Estado != SD.EstadoOrdenPagado &&
                             f.Estado != SD.EstadoOrdenCancelado &&
                             (f.Categoria == "General" || f.MesaId.HasValue || f.OrigenPedido == SD.OrigenPedidoDelivery));

        var totalMesas = await _context.Mesas.AsNoTracking().CountAsync(m => m.Activo);
        var mesasOcupadas = await _context.Mesas.AsNoTracking().CountAsync(m => m.Activo && m.Estado == SD.EstadoMesaOcupada);
        var mesasLibres = await _context.Mesas.AsNoTracking().CountAsync(m => m.Activo && m.Estado == SD.EstadoMesaLibre);

        var facturasPagadas = await _context.Facturas
            .AsNoTracking()
            .Where(f => f.Estado == SD.EstadoOrdenPagado &&
                        (f.Categoria == "General" || f.MesaId.HasValue || f.OrigenPedido == SD.OrigenPedidoDelivery) &&
                        f.FechaPagado.HasValue &&
                        f.FechaPagado.Value >= inicioRango &&
                        f.FechaPagado.Value <= finRango)
            .ToListAsync();

        var ventasHoyFacturas = facturasPagadas
            .Where(f => f.FechaPagado!.Value >= inicioHoy && f.FechaPagado!.Value <= finHoy)
            .ToList();

        var facturaIdsRango = facturasPagadas.Select(f => f.Id).ToHashSet();
        var pagosRango = await _context.Pagos
            .AsNoTracking()
            .Include(p => p.PagoFacturas)
            .Where(p => (p.FacturaId.HasValue && facturaIdsRango.Contains(p.FacturaId.Value))
                || p.PagoFacturas.Any(pf => facturaIdsRango.Contains(pf.FacturaId)))
            .ToListAsync();
        var netoPorFactura = CobroFacturaHelper.NetoCobradoPorFactura(facturaIdsRango, pagosRango);

        decimal NetoCobrado(IEnumerable<Models.Entities.Factura> facturas) =>
            Math.Round(facturas.Sum(f => netoPorFactura.GetValueOrDefault(f.Id)), 2, MidpointRounding.AwayFromZero);

        var totalVentasHoy = NetoCobrado(ventasHoyFacturas);
        var totalOrdenesHoy = ventasHoyFacturas.Count;
        var ticketPromedioHoy = totalOrdenesHoy > 0 ? totalVentasHoy / totalOrdenesHoy : 0;
        var ventasSemana = NetoCobrado(facturasPagadas.Where(f => f.FechaPagado!.Value >= inicioSemana));
        var ventasMes = NetoCobrado(facturasPagadas.Where(f => f.FechaPagado!.Value >= inicioMes));
        var tiempoPromedioPreparacionHoy = facturasPagadas
            .Where(f => f.FechaPagado!.Value >= inicioHoy && f.TiempoPreparacion > 0)
            .Average(f => (double?)f.TiempoPreparacion) ?? 0;

        var serieVentas = facturasPagadas
            .GroupBy(f => f.FechaPagado!.Value.Date)
            .Select(g => new
            {
                Fecha = g.Key.ToString("dd/MM"),
                Monto = Math.Round(g.Sum(x => netoPorFactura.GetValueOrDefault(x.Id)), 2, MidpointRounding.AwayFromZero),
                Ordenes = g.Count()
            })
            .OrderBy(x => x.Fecha)
            .ToList();

        var top = await _context.FacturaServicios
            .AsNoTracking()
            .Include(i => i.Factura)
            .Include(i => i.Servicio)
            .Where(i => i.Factura.Estado == SD.EstadoOrdenPagado &&
                        i.Factura.FechaPagado.HasValue &&
                        i.Factura.FechaPagado.Value >= inicioRango &&
                        i.Factura.FechaPagado.Value <= finRango &&
                        (i.Factura.Categoria == "General" || i.Factura.MesaId.HasValue || i.Factura.OrigenPedido == SD.OrigenPedidoDelivery))
            .GroupBy(i => new { i.ServicioId, i.Servicio.Nombre })
            .Select(g => new
            {
                ProductoId = g.Key.ServicioId,
                Producto = g.Key.Nombre,
                Cantidad = g.Sum(x => x.Cantidad),
                Venta = g.Sum(x => x.Monto)
            })
            .OrderByDescending(x => x.Cantidad)
            .Take(topProductos)
            .ToListAsync();

        var ventasPorCategoriaReal = await _context.FacturaServicios
            .AsNoTracking()
            .Include(fs => fs.Factura)
            .Include(fs => fs.Servicio).ThenInclude(s => s.CategoriaProducto)
            .Where(fs => fs.Factura.Estado == SD.EstadoOrdenPagado &&
                         fs.Factura.FechaPagado.HasValue &&
                         fs.Factura.FechaPagado.Value >= inicioRango &&
                         fs.Factura.FechaPagado.Value <= finRango &&
                         (fs.Factura.Categoria == "General" || fs.Factura.MesaId.HasValue || fs.Factura.OrigenPedido == SD.OrigenPedidoDelivery))
            .Select(g => new
            {
                Categoria = g.Servicio.CategoriaProducto != null ? g.Servicio.CategoriaProducto.Nombre : (g.Servicio.Categoria ?? "Sin categoría"),
                Monto = g.Monto,
                Cantidad = g.Cantidad
            })
            .ToListAsync();

        var categoriasActivas = await _context.CategoriasProducto
            .AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Nombre)
            .ToListAsync();

        var ventasPorCategoria = categoriasActivas
            .Select(cat =>
            {
                var match = ventasPorCategoriaReal.Where(v => v.Categoria == cat.Nombre).ToList();
                return new
                {
                    NombreCategoria = cat.Nombre,
                    Total = match.Sum(x => x.Monto),
                    Cantidad = match.Sum(x => x.Cantidad),
                    ColorHex = "#3B82F6",
                    Icono = string.IsNullOrWhiteSpace(cat.IconoNombre) ? "📦" : cat.IconoNombre
                };
            })
            .Where(v => v.Total > 0 || categoriasActivas.Count <= 10)
            .OrderByDescending(v => v.Total)
            .Take(10)
            .ToList();

        var totalProductos = await _context.Servicios.AsNoTracking().CountAsync(p => p.Activo);
        var productosConStock = await _context.Servicios.AsNoTracking().CountAsync(p => p.Activo && p.ControlarStock && p.Stock > 0);
        var productosStockBajoLista = await _context.Servicios
            .AsNoTracking()
            .Where(p => p.Activo && p.ControlarStock && p.StockMinimo > 0 && p.Stock <= p.StockMinimo)
            .OrderBy(p => p.Stock)
            .Take(5)
            .Select(p => new { p.Nombre, p.Stock, p.StockMinimo })
            .ToListAsync();
        var valorInventario = await _context.Servicios
            .AsNoTracking()
            .Where(p => p.Activo && p.ControlarStock)
            .SumAsync(p => (decimal?)p.Stock * p.Precio) ?? 0;

        return new
        {
            Rango = new { Desde = inicioRango, Hasta = finRango },
            Kpis = new
            {
                CajaAbierta = cajaHoy != null && cajaHoy.Estado == "Abierto",
                MontoInicialCaja = cajaHoy?.MontoInicial ?? 0,
                TotalCajaHoy = totalCajaHoyNeto,
                TotalVentasHoy = totalVentasHoy,
                TotalOrdenesHoy = totalOrdenesHoy,
                TicketPromedioHoy = ticketPromedioHoy,
                VentasSemana = ventasSemana,
                VentasMes = ventasMes,
                TiempoPromedioPreparacion = tiempoPromedioPreparacionHoy,
                OrdenesPendientesPago = ordenesPendientesPago,
                TotalMesas = totalMesas,
                MesasOcupadas = mesasOcupadas,
                MesasLibres = mesasLibres,
                TotalProductos = totalProductos,
                ProductosConStock = productosConStock,
                ProductosStockBajo = productosStockBajoLista.Count,
                ValorInventario = valorInventario
            },
            SerieVentas = serieVentas,
            TopProductos = top,
            VentasPorCategoria = ventasPorCategoria,
            ProductosStockBajoLista = productosStockBajoLista
        };
    }
}
