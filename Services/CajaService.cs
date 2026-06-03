using BarRestPOS.Data;
using BarRestPOS.Models.Api;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class CajaService : ICajaService
{
    private readonly ApplicationDbContext _context;

    public CajaService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EstadoCajaResponse> ObtenerEstadoActualAsync()
    {
        var cierre = await _context.CierresCaja
            .AsNoTracking()
            .Include(c => c.Usuario)
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefaultAsync();

        return new EstadoCajaResponse
        {
            Abierta = cierre != null && cierre.Estado == "Abierto",
            Cierre = cierre
        };
    }

    public async Task<List<object>> ObtenerOrdenesPendientesAsync()
    {
        return await _context.Facturas
            .AsNoTracking()
            .Include(f => f.Mesa)
            .Include(f => f.Cliente)
            .Where(f => f.Estado != SD.EstadoOrdenPagado && f.Estado != SD.EstadoOrdenCancelado)
            .OrderByDescending(f => f.FechaCreacion)
            .Select(f => (object)new
            {
                f.Id,
                f.Numero,
                Mesa = f.Mesa != null ? f.Mesa.Numero : "S/M",
                Cliente = f.Cliente != null ? f.Cliente.Nombre : "General",
                f.Monto,
                f.Estado,
                f.FechaCreacion
            })
            .ToListAsync();
    }

    public async Task<CierreCaja> AbrirCajaAsync(decimal montoInicial, int usuarioId)
    {
        if (montoInicial <= 0) throw new Exception("Monto inicial debe ser mayor a 0.");

        var hayAbierta = await _context.CierresCaja.AnyAsync(c => c.Estado == "Abierto");
        if (hayAbierta) throw new Exception("Ya existe una caja abierta en el sistema. Debe cerrar la caja actual antes de abrir una nueva.");

        var hoy = DateTime.Today;
        var cierre = await _context.CierresCaja
            .Where(c => c.FechaCierre.Date == hoy)
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefaultAsync();

        if (cierre == null)
        {
            cierre = new CierreCaja
            {
                FechaCierre = hoy,
                FechaHoraCierre = DateTime.Now,
                UsuarioId = usuarioId,
                MontoInicial = montoInicial,
                Estado = "Abierto",
                TotalEfectivo = 0,
                TotalTarjeta = 0,
                TotalTransferencia = 0,
                TotalCordobas = 0,
                TotalDolares = 0,
                TotalGeneral = 0,
                TotalOrdenes = 0,
                TotalPagos = 0,
                MontoEsperado = montoInicial
            };
            _context.CierresCaja.Add(cierre);
        }
        else
        {
            cierre.Estado = "Abierto";
            cierre.MontoInicial = montoInicial;
            cierre.UsuarioId = usuarioId;
            cierre.FechaHoraCierre = DateTime.Now;
            cierre.TotalEfectivo = 0;
            cierre.TotalTarjeta = 0;
            cierre.TotalTransferencia = 0;
            cierre.TotalCordobas = 0;
            cierre.TotalDolares = 0;
            cierre.TotalGeneral = 0;
            cierre.TotalOrdenes = 0;
            cierre.TotalPagos = 0;
            cierre.MontoEsperado = montoInicial;
            cierre.MontoReal = null;
            cierre.Diferencia = null;
            cierre.Observaciones = null;
        }

        await _context.SaveChangesAsync();
        return cierre;
    }

    public async Task<PreviewCierreCajaResponse> ObtenerPreviewCierreAsync()
    {
        var cierre = await _context.CierresCaja
            .AsNoTracking()
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefaultAsync(c => c.Estado == "Abierto");
        if (cierre == null) throw new Exception("No hay ninguna caja abierta en el sistema.");

        var inicio = cierre.FechaHoraCierre;
        var fin = DateTime.Now;

        var ordenesPagadas = await _context.Facturas
            .AsNoTracking()
            .Where(f => f.Estado == SD.EstadoOrdenPagado &&
                        (f.Categoria == "General" || f.MesaId.HasValue || f.OrigenPedido == SD.OrigenPedidoDelivery) &&
                        f.FechaPagado >= inicio && f.FechaPagado <= fin)
            .ToListAsync();

        var pagos = await _context.Pagos
            .AsNoTracking()
            .Where(p => p.FechaPago >= inicio && p.FechaPago <= fin)
            .ToListAsync();

        var tipoCambio = decimal.TryParse(await _context.Configuraciones
                .AsNoTracking()
                .Where(c => c.Clave == "TipoCambioDolar")
                .Select(c => c.Valor)
                .FirstOrDefaultAsync(), out var tc)
            ? tc
            : SD.TipoCambioDolar;

        var totalEfectivo = Math.Round(CajaArqueoHelper.TotalEfectivoNetoArqueo(pagos, tipoCambio), 2, MidpointRounding.AwayFromZero);
        var totalTarjeta = pagos.Where(p => p.TipoPago == "Tarjeta").Sum(p => p.Monto);
        var totalTransferencia = pagos.Where(p => p.TipoPago == "Transferencia").Sum(p => p.Monto);
        var totalCordobas = pagos.Sum(p =>
            (p.MontoCordobasFisico ?? 0) +
            (p.MontoCordobasElectronico ?? 0) +
            (p.Moneda == SD.MonedaCordoba ? p.Monto : 0));
        var totalDolares = pagos.Sum(p =>
            (p.MontoDolaresFisico ?? 0) +
            (p.MontoDolaresElectronico ?? 0) +
            (p.Moneda == SD.MonedaDolar ? p.Monto : 0));

        var totalGeneral = Math.Round(pagos.Sum(p => p.Monto), 2, MidpointRounding.AwayFromZero);
        var montoInicial = cierre.MontoInicial ?? 0;
        var montoEsperado = Math.Round(montoInicial + totalEfectivo, 2, MidpointRounding.AwayFromZero);

        return new PreviewCierreCajaResponse
        {
            CierreId = cierre.Id,
            FechaCierre = cierre.FechaCierre,
            Estado = cierre.Estado,
            MontoInicial = montoInicial,
            TotalVentasNetas = totalGeneral,
            TotalEfectivo = totalEfectivo,
            TotalTarjeta = totalTarjeta,
            TotalTransferencia = totalTransferencia,
            TotalCordobas = totalCordobas,
            TotalDolares = totalDolares,
            TotalGeneral = totalGeneral,
            TotalOrdenes = ordenesPagadas.Count,
            TotalPagos = pagos.Count,
            MontoEsperado = montoEsperado
        };
    }

    public async Task<CierreCaja> CerrarCajaAsync(decimal? montoReal, string? observaciones)
    {
        var cierre = await _context.CierresCaja
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefaultAsync(c => c.Estado == "Abierto");
        if (cierre == null) throw new Exception("No hay ninguna sesión de caja abierta en el sistema para cerrar.");

        var preview = await ObtenerPreviewCierreAsync();

        cierre.TotalEfectivo = preview.TotalEfectivo;
        cierre.TotalTarjeta = preview.TotalTarjeta;
        cierre.TotalTransferencia = preview.TotalTransferencia;
        cierre.TotalCordobas = preview.TotalCordobas;
        cierre.TotalDolares = preview.TotalDolares;
        cierre.TotalGeneral = preview.TotalGeneral;
        cierre.TotalOrdenes = preview.TotalOrdenes;
        cierre.TotalPagos = preview.TotalPagos;
        cierre.MontoEsperado = preview.MontoEsperado;
        cierre.MontoReal = montoReal;
        cierre.Diferencia = montoReal.HasValue ? montoReal.Value - preview.MontoEsperado : null;
        cierre.Observaciones = observaciones;
        cierre.Estado = "Cerrado";
        cierre.FechaHoraCierre = DateTime.Now;

        await _context.SaveChangesAsync();

        // Generar un respaldo automático al realizar el cierre de caja
        BarRestPOS.Utils.BackupHelper.CrearRespaldo("cierre");

        return cierre;
    }

    private IQueryable<CierreCaja> BuildHistorialQuery(DateTime? desde, DateTime? hasta)
    {
        var q = _context.CierresCaja
            .AsNoTracking()
            .Include(c => c.Usuario)
            .AsQueryable();
        if (desde.HasValue) q = q.Where(c => c.FechaCierre >= desde.Value.Date);
        if (hasta.HasValue) q = q.Where(c => c.FechaCierre <= hasta.Value.Date);
        return q.OrderByDescending(c => c.FechaCierre).ThenByDescending(c => c.FechaHoraCierre);
    }

    public async Task<PagedResult<CierreCaja>> ObtenerHistorialAsync(int page, int pageSize, DateTime? desde = null, DateTime? hasta = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 5) pageSize = 5;
        if (pageSize > 100) pageSize = 100;

        var query = BuildHistorialQuery(desde, hasta);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<CierreCaja>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        };
    }

    public Task<List<CierreCaja>> ObtenerHistorialParaExportAsync(DateTime? desde, DateTime? hasta)
        => BuildHistorialQuery(desde, hasta).ToListAsync();

    public Task<CierreCaja?> ObtenerCierrePorIdAsync(int id) =>
        _context.CierresCaja.AsNoTracking().Include(c => c.Usuario).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<List<object>> ObtenerPagosPorFechaCierreAsync(DateTime fechaCierre)
    {
        var inicio = fechaCierre.Date;
        var fin = fechaCierre.Date.AddDays(1).AddSeconds(-1);

        return await _context.Pagos
            .AsNoTracking()
            .Include(p => p.Factura).ThenInclude(f => f.Mesa)
            .Include(p => p.Factura).ThenInclude(f => f.Mesero)
            .Where(p => p.FechaPago >= inicio && p.FechaPago <= fin)
            .OrderBy(p => p.FechaPago)
            .Select(p => (object)new
            {
                p.Id,
                p.FechaPago,
                p.TipoPago,
                p.Moneda,
                p.Monto,
                p.MontoRecibido,
                p.Vuelto,
                Orden = p.Factura != null ? p.Factura.Numero : null,
                Mesa = p.Factura != null && p.Factura.Mesa != null ? p.Factura.Mesa.Numero : null,
                Mesero = p.Factura != null && p.Factura.Mesero != null ? p.Factura.Mesero.NombreCompleto : null
            })
            .ToListAsync();
    }
}
