using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BarRestPOS.Services;

/// <summary>
/// Cancelación de pedidos con devolución selectiva de inventario (solo productos con ControlarStock).
/// El PIN se valida en el controlador antes de llamar a <see cref="EjecutarCancelacion"/>.
/// </summary>
public class PedidoCancelacionService
{
    private readonly ApplicationDbContext _context;
    private readonly IInventarioService _inventarioService;
    private readonly IAuditService _auditService;

    public PedidoCancelacionService(ApplicationDbContext context, IInventarioService inventarioService, IAuditService auditService)
    {
        _context = context;
        _inventarioService = inventarioService;
        _auditService = auditService;
    }

    /// <summary>Devuelve null si OK; mensaje de error si no se puede cancelar.</summary>
    public string? EjecutarCancelacion(int pedidoId, int usuarioId)
    {
        var pedido = _context.Facturas
            .Include(f => f.FacturaServicios)
            .ThenInclude(l => l.Servicio)
            .FirstOrDefault(f => f.Id == pedidoId);

        if (pedido == null)
            return "Pedido no encontrado.";

        if (pedido.Estado == SD.EstadoOrdenPagado)
            return "No se puede cancelar un pedido pagado.";

        if (pedido.Estado == SD.EstadoOrdenCancelado)
            return "El pedido ya está cancelado.";

        foreach (var linea in pedido.FacturaServicios)
        {
            var svc = linea.Servicio;
            if (svc == null)
                continue;
            if (!svc.ControlarStock)
                continue;
            var cant = linea.Cantidad;
            if (cant <= 0)
                continue;
            _inventarioService.AplicarEntradaDevolucionCancelacionSinGuardar(
                svc.Id,
                cant,
                pedido.Id,
                usuarioId,
                $"Cancelación pedido {pedido.Numero}");
        }

        pedido.Estado = SD.EstadoOrdenCancelado;
        pedido.FechaActualizacion = DateTime.Now;

        LiberarMesaSiAplica(pedido);

        try
        {
            _context.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            return "El pedido fue modificado por otro usuario. Recargue e intente de nuevo.";
        }

        // Registrar acción en la bitácora de auditoría
        try
        {
            var mesaNum = pedido.MesaId.HasValue
                ? (_context.Mesas.FirstOrDefault(m => m.Id == pedido.MesaId.Value)?.Numero ?? $"Mesa {pedido.MesaId.Value}")
                : "Delivery/Llevar";

            _auditService.RegistrarAccionAsync(
                "CancelacionPedido",
                mesaNum,
                pedido.Id,
                new { numero = pedido.Numero, monto = pedido.Monto },
                usuarioId
            ).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al registrar auditoria de cancelacion de pedido: {ex.Message}");
        }

        return null;
    }

    private void LiberarMesaSiAplica(Factura pedido)
    {
        if (!pedido.MesaId.HasValue)
            return;
        var otros = _context.Facturas.Count(f =>
            f.MesaId == pedido.MesaId && f.Id != pedido.Id
            && f.Estado != SD.EstadoOrdenPagado
            && f.Estado != SD.EstadoOrdenCancelado);
        if (otros != 0)
            return;
        var mesa = _context.Mesas.FirstOrDefault(m => m.Id == pedido.MesaId.Value);
        if (mesa != null)
            mesa.Estado = SD.EstadoMesaLibre;
    }
}
