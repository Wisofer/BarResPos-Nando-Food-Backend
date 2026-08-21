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
    private readonly IAuditService _auditService;

    public CajaService(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
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

    private async Task LimpiarBorradoresVaciosAsync()
    {
        var ordenesVacias = await _context.Facturas
            .Include(f => f.FacturaServicios)
            .Where(f => f.Estado != SD.EstadoOrdenPagado 
                     && f.Estado != SD.EstadoOrdenCancelado 
                     && (f.Monto <= 0 || !f.FacturaServicios.Any()))
            .ToListAsync();

        if (ordenesVacias.Count > 0)
        {
            foreach (var ov in ordenesVacias)
            {
                ov.Estado = SD.EstadoOrdenCancelado;
                if (ov.MesaId.HasValue)
                {
                    var otros = await _context.Facturas.AnyAsync(f => f.MesaId == ov.MesaId.Value && f.Id != ov.Id && f.Estado != SD.EstadoOrdenPagado && f.Estado != SD.EstadoOrdenCancelado);
                    if (!otros)
                    {
                        var m = await _context.Mesas.FirstOrDefaultAsync(m => m.Id == ov.MesaId.Value);
                        if (m != null) m.Estado = SD.EstadoMesaLibre;
                    }
                }
            }
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<object>> ObtenerOrdenesPendientesAsync()
    {
        await LimpiarBorradoresVaciosAsync();

        return await _context.Facturas
            .AsNoTracking()
            .Include(f => f.Mesa)
            .Include(f => f.Cliente)
            .Include(f => f.FacturaServicios)
            .Where(f => f.Estado != SD.EstadoOrdenPagado && f.Estado != SD.EstadoOrdenCancelado)
            .OrderByDescending(f => f.FechaCreacion)
            .Select(f => (object)new
            {
                f.Id,
                f.Numero,
                Mesa = f.Mesa != null ? f.Mesa.Numero : (!string.IsNullOrEmpty(f.OrigenPedido) && f.OrigenPedido.ToLower() != "salon" ? f.OrigenPedido : "S/M"),
                Cliente = f.Cliente != null ? f.Cliente.Nombre : "General",
                f.Monto,
                f.Estado,
                f.FechaCreacion,
                CantidadProductos = f.FacturaServicios.Sum(fs => fs.Cantidad)
            })
            .ToListAsync();
    }

    public async Task<CierreCaja> AbrirCajaAsync(decimal montoInicial, int usuarioId)
    {
        if (montoInicial <= 0) throw new Exception("Monto inicial debe ser mayor a 0.");

        var hayAbierta = await _context.CierresCaja.AnyAsync(c => c.Estado == "Abierto");
        if (hayAbierta) throw new Exception("Ya existe una caja abierta en el sistema. Debe cerrar la caja actual antes de abrir una nueva.");

        var hoy = DateTime.Today;
        var ahora = DateTime.Now;
        var cierre = new CierreCaja
        {
            FechaCierre = hoy,
            FechaHoraApertura = ahora,
            FechaHoraCierre = ahora,
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
        await _context.SaveChangesAsync();

        // Registrar acción en la bitácora de auditoría
        await _auditService.RegistrarAccionAsync(
            "AperturaCaja",
            "Caja",
            cierre.Id,
            new { montoInicial = montoInicial },
            usuarioId
        );

        return cierre;
    }

    public async Task<PreviewCierreCajaResponse> ObtenerPreviewCierreAsync()
    {
        var cierre = await _context.CierresCaja
            .AsNoTracking()
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefaultAsync(c => c.Estado == "Abierto");
        if (cierre == null) throw new Exception("No hay ninguna caja abierta en el sistema.");

        var inicio = cierre.FechaHoraApertura;
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
        
        var totalCordobas = Math.Round(pagos.Sum(p =>
        {
            if (p.MontoCordobasFisico.HasValue || p.MontoCordobasElectronico.HasValue)
            {
                return (p.MontoCordobasFisico ?? 0) + (p.MontoCordobasElectronico ?? 0);
            }
            return p.Moneda == SD.MonedaCordoba ? p.Monto : 0m;
        }), 2, MidpointRounding.AwayFromZero);

        var totalDolares = Math.Round(pagos.Sum(p =>
        {
            if (p.MontoDolaresFisico.HasValue || p.MontoDolaresElectronico.HasValue)
            {
                return (p.MontoDolaresFisico ?? 0) + (p.MontoDolaresElectronico ?? 0);
            }
            if (p.Moneda == SD.MonedaDolar)
            {
                var tcPago = p.TipoCambio ?? tipoCambio;
                return p.Monto / (tcPago > 0 ? tcPago : SD.TipoCambioDolar);
            }
            return 0m;
        }), 2, MidpointRounding.AwayFromZero);

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

    public async Task<CierreCaja> CerrarCajaAsync(decimal? montoReal, string? observaciones, int usuarioId)
    {
        var cierre = await _context.CierresCaja
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefaultAsync(c => c.Estado == "Abierto");
        if (cierre == null) throw new Exception("No hay ninguna sesión de caja abierta en el sistema para cerrar.");

        await LimpiarBorradoresVaciosAsync();

        var preview = await ObtenerPreviewCierreAsync();

        var pendientesCount = await _context.Facturas
            .CountAsync(f => f.Estado != SD.EstadoOrdenPagado && f.Estado != SD.EstadoOrdenCancelado);
        if (pendientesCount > 0)
            throw new Exception(
                $"No se puede cerrar la caja porque hay {pendientesCount} orden(es) pendiente(s) de pago. " +
                "Debe procesar o cancelar todas las órdenes antes de cerrar.");

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
        cierre.UsuarioId = usuarioId;

        await _context.SaveChangesAsync();

        // Registrar acción en la bitácora de auditoría
        await _auditService.RegistrarAccionAsync(
            "CierreCaja",
            "Caja",
            cierre.Id,
            new { montoEsperado = cierre.MontoEsperado, montoReal = cierre.MontoReal, diferencia = cierre.Diferencia, totalGeneral = cierre.TotalGeneral },
            usuarioId
        );

        if (cierre.Diferencia.HasValue && cierre.Diferencia.Value != 0)
        {
            await _auditService.RegistrarAccionAsync(
                "DiferenciaCierre",
                "Caja",
                cierre.Id,
                new { diferencia = cierre.Diferencia.Value, esperado = cierre.MontoEsperado, real = cierre.MontoReal },
                usuarioId
            );
        }

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

    public async Task<List<object>> ObtenerPagosPorCierreAsync(CierreCaja cierre)
    {
        var inicio = cierre.FechaHoraApertura;
        var fin = cierre.FechaHoraCierre;

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
