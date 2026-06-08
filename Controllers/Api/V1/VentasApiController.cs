using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/ventas")]
public class VentasApiController : BaseApiController
{
    private const decimal ToleranciaMonto = 0.02m;

    private readonly ApplicationDbContext _context;

    public VentasApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [Authorize(Policy = "Cajero")]
    [HttpPost("gestionar-pago")]
    public IActionResult GestionarPago([FromBody] GestionarPagoVentaRequest request)
    {
        return EjecutarPago(request, "Pago gestionado");
    }

    [Authorize(Policy = "Cajero")]
    [HttpPost("procesar-pago")]
    public IActionResult ProcesarPago([FromBody] ProcesarPagoVentaRequest request)
    {
        return EjecutarPago(request, "Pago procesado");
    }

    private IActionResult EjecutarPago(ProcesarPagoVentaRequest request, string mensajeExito)
    {
        if (request.OrdenId <= 0) return FailResponse("Orden inválida.");
        if (string.IsNullOrWhiteSpace(request.TipoPago)) return FailResponse("Tipo de pago es requerido.");

        var descuento = request.DescuentoMonto ?? 0m;
        descuento = Math.Round(descuento, 2, MidpointRounding.AwayFromZero);
        if (descuento < 0)
            return FailResponse("El descuento no puede ser negativo.", StatusCodes.Status400BadRequest);

        var cierre = _context.CierresCaja
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefault(c => c.Estado == "Abierto");
        if (cierre == null) return FailResponse("La caja está cerrada. Un administrador debe abrir la caja primero.", StatusCodes.Status409Conflict);

        var orden = _context.Facturas
            .Include(f => f.Mesa)
            .Include(f => f.FacturaServicios)
            .FirstOrDefault(f => f.Id == request.OrdenId);
        if (orden == null) return FailResponse("Orden no encontrada.", StatusCodes.Status404NotFound);
        if (orden.Estado == SD.EstadoOrdenPagado) return FailResponse("La orden ya fue pagada.");
        if (orden.Estado == SD.EstadoOrdenCancelado) return FailResponse("La orden fue cancelada y no puede ser procesada.");
        if (!orden.FacturaServicios.Any())
            return FailResponse("No se puede procesar/cobrar un pedido sin items.", StatusCodes.Status400BadRequest);

        var subtotalPedido = Math.Round(orden.Monto, 2, MidpointRounding.AwayFromZero);
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
            FacturaId = orden.Id,
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
        orden.Estado = SD.EstadoOrdenPagado;
        orden.FechaPagado = DateTime.Now;
        orden.FechaActualizacion = DateTime.Now;

        if (orden.MesaId.HasValue)
        {
            var mesa = _context.Mesas.FirstOrDefault(m => m.Id == orden.MesaId.Value);
            if (mesa != null) 
            {
                bool hayOtrasActivas = _context.Facturas.Any(f => 
                    f.MesaId == mesa.Id && 
                    f.Id != orden.Id && 
                    f.Estado != SD.EstadoOrdenPagado && 
                    f.Estado != "Cancelado");
                
                if (!hayOtrasActivas)
                {
                    mesa.Estado = SD.EstadoMesaLibre;
                }
            }
        }

        _context.SaveChanges();

        return OkResponse(new
        {
            pago.Id,
            OrdenNumero = orden.Numero,
            Vuelto = pago.Vuelto,
            UrlImpresionRecibo = $"/api/v1/impresion/recibo/{pago.Id}",
            SubtotalPedidoCordobas = subtotalPedido,
            DescuentoCordobas = descuento,
            TotalNetoCordobas = totalNetoCordobas,
            TotalCordobas = totalNetoCordobas,
            MontoPagadoCordobas = montoPagadoCordobas,
            VueltoCordobas = pago.Vuelto,
            TipoCambioAplicado = tipoCambio
        }, mensajeExito);
    }

    private static string? ConstruirObservacionesPago(string? observacionesCliente, decimal descuento, string? motivo)
    {
        var partes = new List<string>();
        if (descuento > 0)
        {
            var linea = $"Descuento C$ {descuento:N2}";
            if (!string.IsNullOrWhiteSpace(motivo))
                linea += $" — {motivo.Trim()}";
            partes.Add(linea);
        }
        if (!string.IsNullOrWhiteSpace(observacionesCliente))
            partes.Add(observacionesCliente.Trim());
        return partes.Count == 0 ? null : string.Join(" | ", partes);
    }
}

public class ProcesarPagoVentaRequest
{
    public int OrdenId { get; set; }
    public string TipoPago { get; set; } = "Efectivo";
    public decimal MontoPagado { get; set; }
    public string? Moneda { get; set; }
    public string? Banco { get; set; }
    public string? TipoCuenta { get; set; }
    public string? Observaciones { get; set; }

    /// <summary>Descuento en córdobas aplicado solo al cobro (≥ 0). El total a pagar es subtotal pedido − descuento.</summary>
    public decimal? DescuentoMonto { get; set; }

    /// <summary>Motivo del descuento (auditoría; también se resume en observaciones del pago).</summary>
    public string? DescuentoMotivo { get; set; }

    public decimal? MontoCordobasFisico { get; set; }
    public decimal? MontoDolaresFisico { get; set; }
    public decimal? MontoCordobasElectronico { get; set; }
    public decimal? MontoDolaresElectronico { get; set; }
}

public class GestionarPagoVentaRequest : ProcesarPagoVentaRequest
{
}
