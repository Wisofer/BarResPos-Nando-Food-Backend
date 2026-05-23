using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class PagoService : IPagoService
{
    private readonly ApplicationDbContext _context;
    private readonly IFacturaService _facturaService;
    private readonly IConfiguracionService _configuracionService;

    public PagoService(ApplicationDbContext context, IFacturaService facturaService, IConfiguracionService configuracionService)
    {
        _context = context;
        _facturaService = facturaService;
        _configuracionService = configuracionService;
    }

    public List<Pago> ObtenerTodos()
    {
        return _context.Pagos
            .Include(p => p.Factura)
            .ThenInclude(f => f!.Cliente)
            .Include(p => p.PagoFacturas)
            .ThenInclude(pf => pf.Factura)
            .ThenInclude(f => f!.Cliente)
            .OrderByDescending(p => p.FechaPago)
            .ToList();
    }

    public Pago? ObtenerPorId(int id)
    {
        return _context.Pagos
            .Include(p => p.Factura)
            .ThenInclude(f => f!.Cliente)
            .Include(p => p.PagoFacturas)
            .ThenInclude(pf => pf.Factura)
            .ThenInclude(f => f!.Cliente)
            .FirstOrDefault(p => p.Id == id);
    }

    public List<Pago> ObtenerPorFactura(int facturaId)
    {
        // Buscar pagos que tengan esta factura directamente o a través de PagoFactura
        return _context.Pagos
            .Include(p => p.Factura)
            .ThenInclude(f => f!.Cliente)
            .Include(p => p.PagoFacturas)
            .ThenInclude(pf => pf.Factura)
            .ThenInclude(f => f!.Cliente)
            .Where(p => p.FacturaId == facturaId || p.PagoFacturas.Any(pf => pf.FacturaId == facturaId))
            .OrderByDescending(p => p.FechaPago)
            .ToList();
    }

    public decimal CalcularVuelto(decimal montoRecibido, decimal costo)
    {
        return montoRecibido > costo ? montoRecibido - costo : 0;
    }

    public decimal ConvertirMoneda(decimal monto, string monedaOrigen, string monedaDestino)
    {
        if (monedaOrigen == monedaDestino)
            return monto;

        var tipoCambio = _configuracionService.ObtenerValorDecimal("TipoCambioDolar") ?? SD.TipoCambioDolar;

        if (monedaOrigen == SD.MonedaCordoba && monedaDestino == SD.MonedaDolar)
            return monto / tipoCambio;

        if (monedaOrigen == SD.MonedaDolar && monedaDestino == SD.MonedaCordoba)
            return monto * tipoCambio;

        return monto;
    }

    /// <summary>
    /// Calcula el monto total del pago considerando pagos mixtos y múltiples monedas
    /// </summary>
    public decimal CalcularMontoTotal(Pago pago)
    {
        decimal total = 0;
        var tipoCambioDefault = _configuracionService.ObtenerValorDecimal("TipoCambioDolar") ?? SD.TipoCambioDolar;
        var tipoCambio = pago.TipoCambio ?? tipoCambioDefault;

        if (pago.TipoPago == SD.TipoPagoMixto)
        {
            // Pago mixto: sumar físico + electrónico
            // Parte física
            var montoFisico = (pago.MontoCordobasFisico ?? 0) + 
                             ((pago.MontoDolaresFisico ?? 0) * tipoCambio);
            
            // Parte electrónica
            var montoElectronico = (pago.MontoCordobasElectronico ?? 0) + 
                                  ((pago.MontoDolaresElectronico ?? 0) * tipoCambio);
            
            total = montoFisico + montoElectronico;
        }
        else if (pago.TipoPago == SD.TipoPagoFisico)
        {
            // Pago físico: usar nuevos campos si existen, sino usar campos antiguos
            if (pago.MontoCordobasFisico.HasValue || pago.MontoDolaresFisico.HasValue)
            {
                total = (pago.MontoCordobasFisico ?? 0) + 
                       ((pago.MontoDolaresFisico ?? 0) * tipoCambio);
            }
            else
            {
                // Compatibilidad con pagos antiguos
                if (pago.Moneda == SD.MonedaCordoba)
                    total = pago.Monto;
                else if (pago.Moneda == SD.MonedaDolar)
                    total = pago.Monto * tipoCambio;
                else if (pago.Moneda == SD.MonedaAmbos)
                    total = pago.Monto; // Ya debería estar calculado
                else
                    total = pago.Monto;
            }
        }
        else if (pago.TipoPago == SD.TipoPagoElectronico)
        {
            // Pago electrónico: usar nuevos campos si existen, sino usar campos antiguos
            if (pago.MontoCordobasElectronico.HasValue || pago.MontoDolaresElectronico.HasValue)
            {
                total = (pago.MontoCordobasElectronico ?? 0) + 
                       ((pago.MontoDolaresElectronico ?? 0) * tipoCambio);
            }
            else
            {
                // Compatibilidad con pagos antiguos
                if (pago.Moneda == SD.MonedaCordoba)
                    total = pago.Monto;
                else if (pago.Moneda == SD.MonedaDolar)
                    total = pago.Monto * tipoCambio;
                else if (pago.Moneda == SD.MonedaAmbos)
                    total = pago.Monto; // Ya debería estar calculado
                else
                    total = pago.Monto;
            }
        }
        else
        {
            // Fallback: usar monto directo
            total = pago.Monto;
        }

        return total;
    }

    public Pago Crear(Pago pago, List<int>? facturaIds = null, List<decimal>? montosAplicados = null)
    {
        pago.FechaPago = DateTime.Now;
        var tipoCambioDefault = _configuracionService.ObtenerValorDecimal("TipoCambioDolar") ?? SD.TipoCambioDolar;
        pago.TipoCambio = pago.TipoCambio ?? tipoCambioDefault;
        
        // Calcular el monto total según el tipo de pago y monedas
        pago.Monto = CalcularMontoTotal(pago);

        // Si se proporcionan múltiples facturas, usar PagoFactura
        if (facturaIds != null && facturaIds.Any())
        {
            // Validar que todas las facturas existan y pertenezcan al mismo cliente
            var facturas = facturaIds.Select(id => _facturaService.ObtenerPorId(id))
                .Where(f => f != null)
                .ToList();

            if (facturas.Count != facturaIds.Count)
                throw new Exception("Una o más facturas no fueron encontradas");

            // Verificar que todas las facturas pertenezcan al mismo cliente
            var primerClienteId = facturas.First()!.ClienteId;
            if (facturas.Any(f => f!.ClienteId != primerClienteId))
                throw new Exception("Todas las facturas deben pertenecer al mismo cliente");

            // Si no se proporcionan montos específicos, distribuir proporcionalmente
            if (montosAplicados == null || montosAplicados.Count != facturaIds.Count)
            {
                // Calcular saldos pendientes
                var saldosPendientes = facturas.Select(f =>
                {
                    var totalPagado = ObtenerPorFactura(f!.Id).Sum(p => p.Monto);
                    return f.Monto - totalPagado;
                }).ToList();

                var totalSaldoPendiente = saldosPendientes.Sum();
                if (totalSaldoPendiente <= 0)
                    throw new Exception("No hay saldo pendiente en las facturas seleccionadas");

                // Calcular monto total primero
                var montoTotal = CalcularMontoTotal(pago);
                
                // Distribuir el monto proporcionalmente
                montosAplicados = saldosPendientes.Select(saldo =>
                    saldo > 0 ? (montoTotal * saldo / totalSaldoPendiente) : 0
                ).ToList();
            }

            // Calcular monto total
            var montoTotalPago = CalcularMontoTotal(pago);
            pago.Monto = montoTotalPago;

            // Validar que la suma de montos aplicados no exceda el monto del pago
            var sumaMontos = montosAplicados.Sum();
            if (sumaMontos > montoTotalPago * 1.01m) // Permitir 1% de diferencia por redondeo
                throw new Exception($"La suma de montos aplicados ({sumaMontos:N2}) excede el monto del pago ({montoTotalPago:N2})");

            // Establecer FacturaId como null para pagos con múltiples facturas
            pago.FacturaId = null;

            // Calcular vuelto si es pago físico o mixto
            if (pago.TipoPago == SD.TipoPagoFisico || pago.TipoPago == SD.TipoPagoMixto)
            {
                if (pago.MontoRecibidoFisico.HasValue)
                {
                    var tipoCambio = pago.TipoCambio ?? tipoCambioDefault;
                    var montoFisico = (pago.MontoCordobasFisico ?? 0) + 
                                     ((pago.MontoDolaresFisico ?? 0) * tipoCambio);
                    pago.VueltoFisico = CalcularVuelto(pago.MontoRecibidoFisico.Value, montoFisico);
                }
                else if (pago.MontoRecibido.HasValue && pago.TipoPago == SD.TipoPagoFisico)
                {
                    pago.Vuelto = CalcularVuelto(pago.MontoRecibido.Value, montoTotalPago);
                }
            }

            _context.Pagos.Add(pago);
            _context.SaveChanges();

            // Crear registros PagoFactura
            for (int i = 0; i < facturaIds.Count; i++)
            {
                var pagoFactura = new PagoFactura
                {
                    PagoId = pago.Id,
                    FacturaId = facturaIds[i],
                    MontoAplicado = montosAplicados[i]
                };
                _context.PagoFacturas.Add(pagoFactura);
            }
            _context.SaveChanges();

            // Actualizar estado de cada factura
            foreach (var factura in facturas)
            {
                var totalPagado = ObtenerPorFactura(factura!.Id).Sum(p => p.Monto);
                if (totalPagado >= factura.Monto)
                {
                    factura.Estado = SD.EstadoFacturaPagada;
                }
            }
            _context.SaveChanges();
        }
        else
        {
            // Pago de una sola factura (comportamiento original)
            if (!pago.FacturaId.HasValue)
                throw new Exception("Debe especificar al menos una factura");

            var factura = _facturaService.ObtenerPorId(pago.FacturaId.Value);
            if (factura == null)
                throw new Exception("Factura no encontrada");

            // Calcular vuelto si es pago físico
            if (pago.TipoPago == SD.TipoPagoFisico && pago.MontoRecibido.HasValue)
            {
                pago.Vuelto = CalcularVuelto(pago.MontoRecibido.Value, pago.Monto);
            }

            _context.Pagos.Add(pago);
            _context.SaveChanges();

            // Actualizar estado de factura si está completamente pagada
            var totalPagado = ObtenerPorFactura(factura.Id).Sum(p => p.Monto);
            if (totalPagado >= factura.Monto)
            {
                factura.Estado = SD.EstadoFacturaPagada;
                _context.SaveChanges();
            }
        }

        return pago;
    }

    public Pago Actualizar(Pago pago)
    {
        var existente = ObtenerPorId(pago.Id);
        if (existente == null)
            throw new Exception("Pago no encontrado");

        existente.Moneda = pago.Moneda;
        existente.TipoPago = pago.TipoPago;
        existente.Banco = pago.Banco;
        existente.TipoCuenta = pago.TipoCuenta;
        existente.MontoRecibido = pago.MontoRecibido;
        existente.Vuelto = pago.Vuelto;
        existente.Observaciones = pago.Observaciones;
        
        // Campos para pago físico con múltiples monedas
        existente.MontoCordobasFisico = pago.MontoCordobasFisico;
        existente.MontoDolaresFisico = pago.MontoDolaresFisico;
        existente.MontoRecibidoFisico = pago.MontoRecibidoFisico;
        existente.VueltoFisico = pago.VueltoFisico;
        
        // Campos para pago electrónico con múltiples monedas
        existente.MontoCordobasElectronico = pago.MontoCordobasElectronico;
        existente.MontoDolaresElectronico = pago.MontoDolaresElectronico;
        
        // Recalcular monto total
        existente.Monto = CalcularMontoTotal(pago);

        if (pago.TipoPago == SD.TipoPagoFisico || pago.TipoPago == SD.TipoPagoMixto)
        {
            if (pago.MontoRecibidoFisico.HasValue)
            {
                var montoFisico = (pago.MontoCordobasFisico ?? 0) + 
                                 ((pago.MontoDolaresFisico ?? 0) * (pago.TipoCambio ?? _configuracionService.ObtenerValorDecimal("TipoCambioDolar") ?? SD.TipoCambioDolar));
                existente.VueltoFisico = CalcularVuelto(pago.MontoRecibidoFisico.Value, montoFisico);
            }
            else if (pago.MontoRecibido.HasValue && pago.TipoPago == SD.TipoPagoFisico)
            {
                existente.Vuelto = CalcularVuelto(pago.MontoRecibido.Value, existente.Monto);
            }
        }

        _context.SaveChanges();
        return existente;
    }

    public bool Eliminar(int id)
    {
        var pago = ObtenerPorId(id);
        if (pago == null)
            return false;

        // Obtener facturas afectadas (tanto directas como a través de PagoFactura)
        var facturasAfectadas = new List<int>();
        if (pago.FacturaId.HasValue)
        {
            facturasAfectadas.Add(pago.FacturaId.Value);
        }
        facturasAfectadas.AddRange(pago.PagoFacturas.Select(pf => pf.FacturaId));

        _context.Pagos.Remove(pago);
        _context.SaveChanges();

        // Actualizar estado de todas las facturas afectadas
        foreach (var facturaId in facturasAfectadas.Distinct())
        {
            var factura = _facturaService.ObtenerPorId(facturaId);
            if (factura != null)
            {
                var totalPagado = ObtenerPorFactura(factura.Id).Sum(p => p.Monto);
                if (totalPagado < factura.Monto)
                {
                    factura.Estado = SD.EstadoFacturaPendiente;
                }
            }
        }
        _context.SaveChanges();

        return true;
    }

    public (int eliminados, int noEncontrados) EliminarMultiples(List<int> ids)
    {
        if (ids == null || !ids.Any())
            return (0, 0);

        int eliminados = 0;
        int noEncontrados = 0;
        var facturasARevisar = new HashSet<int>();

        foreach (var id in ids)
        {
            var pago = ObtenerPorId(id);
            if (pago == null)
            {
                noEncontrados++;
                continue;
            }

            // Agregar facturas afectadas (directas y a través de PagoFactura)
            if (pago.FacturaId.HasValue)
            {
                facturasARevisar.Add(pago.FacturaId.Value);
            }
            facturasARevisar.UnionWith(pago.PagoFacturas.Select(pf => pf.FacturaId));
            
            _context.Pagos.Remove(pago);
            eliminados++;
        }

        if (eliminados > 0)
        {
            _context.SaveChanges();

            // Actualizar estado de facturas afectadas
            foreach (var facturaId in facturasARevisar)
            {
                var factura = _facturaService.ObtenerPorId(facturaId);
                if (factura != null)
                {
                    var totalPagado = ObtenerPorFactura(factura.Id).Sum(p => p.Monto);
                    if (totalPagado < factura.Monto)
                    {
                        factura.Estado = SD.EstadoFacturaPendiente;
                    }
                }
            }
            _context.SaveChanges();
        }

        return (eliminados, noEncontrados);
    }

    public decimal CalcularTotalIngresos()
    {
        return _context.Pagos.Sum(p => p.Monto);
    }
}

