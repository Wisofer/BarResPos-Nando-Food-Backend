using BarRestPOS.Data;
using BarRestPOS.Models.Api;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BarRestPOS.Services;

/// <summary>
/// Reemplaza todas las líneas de una orden (inventario + opciones + precios), en una transacción.
/// </summary>
public class OrdenLineasReemplazoService
{
    private readonly ILogger<OrdenLineasReemplazoService> _logger;

    public OrdenLineasReemplazoService(ILogger<OrdenLineasReemplazoService> logger)
    {
        _logger = logger;
    }

    /// <returns>null si OK; mensaje de error en caso contrario.</returns>
    public string? ReemplazarLineas(
        ApplicationDbContext db,
        IInventarioService inventarioService,
        Factura pedido,
        List<ActualizarPedidoItemRequest> items,
        int userId,
        string refPedidoLog)
    {
        if (items.Count == 0)
            return "Debe incluir al menos un item.";

        var cantidadAnteriorPorProducto = pedido.FacturaServicios
            .GroupBy(fs => fs.ServicioId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));

        var cantidadNuevaPorProducto = items
            .GroupBy(i => i.ServicioId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));

        var productoIds = items.Select(i => i.ServicioId).Distinct().ToList();
        var productos = db.Servicios
            .AsNoTracking()
            .Include(s => s.OpcionGrupos)
            .ThenInclude(g => g.Opciones)
            .Where(s => productoIds.Contains(s.Id) && s.Activo)
            .ToDictionary(s => s.Id, s => s);

        if (productos.Count != productoIds.Count)
            return "Uno o más productos no existen o están inactivos.";

        foreach (var item in items)
        {
            if (item.Cantidad <= 0)
                return "La cantidad de cada item debe ser mayor a 0.";
        }

        var idsInventario = cantidadAnteriorPorProducto.Keys.Union(cantidadNuevaPorProducto.Keys).Distinct().ToList();
        var serviciosInventario = db.Servicios
            .Where(s => idsInventario.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s);

        if (serviciosInventario.Count != idsInventario.Count)
            return "Uno o más productos del pedido ya no existen.";

        foreach (var productoId in idsInventario)
        {
            var delta = cantidadNuevaPorProducto.GetValueOrDefault(productoId, 0)
                - cantidadAnteriorPorProducto.GetValueOrDefault(productoId, 0);
            if (delta <= 0) continue;
            var s = serviciosInventario[productoId];
            if (!s.ControlarStock) continue;
            if (!inventarioService.ValidarStockDisponible(productoId, delta))
                return $"Stock insuficiente para {s.Nombre}. Disponible: {s.Stock}, incremento neto solicitado: {delta}.";
        }

        using var tx = db.Database.BeginTransaction();
        try
        {
            foreach (var productoId in idsInventario)
            {
                var delta = cantidadNuevaPorProducto.GetValueOrDefault(productoId, 0)
                    - cantidadAnteriorPorProducto.GetValueOrDefault(productoId, 0);
                if (delta == 0) continue;

                var svc = serviciosInventario[productoId];
                if (!svc.ControlarStock) continue;

                try
                {
                    if (delta > 0)
                    {
                        inventarioService.RegistrarSalida(
                            productoId,
                            delta,
                            SD.SubtipoMovimientoVenta,
                            pedido.Id,
                            $"Pedido {refPedidoLog} — actualización de líneas",
                            userId);
                    }
                    else
                    {
                        inventarioService.RegistrarEntrada(
                            productoId,
                            -delta,
                            null,
                            null,
                            null,
                            $"Devolución por edición pedido {refPedidoLog}",
                            userId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Inventario al actualizar pedido {PedidoId} producto {ProductoId}", pedido.Id, productoId);
                    tx.Rollback();
                    return $"No se pudo actualizar inventario: {ex.Message}";
                }
            }

            db.FacturaServicios.RemoveRange(pedido.FacturaServicios);
            pedido.FacturaServicios.Clear();

            decimal nuevoMonto = 0;
            foreach (var item in items)
            {
                var producto = productos[item.ServicioId];
                var seleccionesDto = (item.OpcionesSeleccionadas ?? new List<OpcionSeleccionRequest>())
                    .Select(o => new OpcionSeleccionDto(o.GrupoId, o.OpcionId))
                    .ToList();
                var gruposEf = ProductoOpcionesLineaHelper.GruposEfectivos(producto.OpcionGrupos);

                decimal precio;
                List<FacturaServicioOpcionSeleccion> filasOpc;
                if (gruposEf.Count == 0 && seleccionesDto.Count == 0)
                {
                    precio = item.PrecioUnitario.HasValue && item.PrecioUnitario.Value >= 0
                        ? item.PrecioUnitario.Value
                        : producto.Precio;
                    filasOpc = new List<FacturaServicioOpcionSeleccion>();
                }
                else
                {
                    var (adicional, filas, errOp) = ProductoOpcionesLineaHelper.ValidarYConstruirFilas(producto, seleccionesDto);
                    if (errOp != null)
                    {
                        tx.Rollback();
                        return errOp;
                    }

                    filasOpc = filas;
                    precio = Math.Round(producto.Precio + adicional, 2, MidpointRounding.AwayFromZero);
                }

                var subtotal = Math.Round(precio * item.Cantidad, 2, MidpointRounding.AwayFromZero);
                nuevoMonto += subtotal;

                var linea = new FacturaServicio
                {
                    FacturaId = pedido.Id,
                    ServicioId = item.ServicioId,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = precio,
                    Monto = subtotal,
                    Estado = string.IsNullOrWhiteSpace(item.Estado) ? SD.EstadoCocinaPendiente : item.Estado.Trim(),
                    Notas = item.Notas
                };
                foreach (var op in filasOpc)
                    linea.OpcionesSeleccionadas.Add(op);
                pedido.FacturaServicios.Add(linea);
            }

            pedido.Monto = nuevoMonto;
            pedido.ServicioId = items[0].ServicioId;
            pedido.FechaActualizacion = DateTime.Now;

            db.SaveChanges();
            tx.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al reemplazar líneas pedido {PedidoId}", pedido.Id);
            return "Error al guardar el pedido.";
        }

        return null;
    }

    /// <summary>
    /// Elimina una línea del pedido, devuelve inventario de esa línea y recalcula el monto.
    /// Si no quedan líneas, deja el pedido vacío (monto 0, mesa desvinculada).
    /// </summary>
    /// <returns>null si OK; mensaje de error si falla.</returns>
    public (bool vacio, string? error) EliminarLinea(
        ApplicationDbContext db,
        IInventarioService inventarioService,
        Factura pedido,
        int lineaId,
        int userId,
        string refPedidoLog)
    {
        var linea = pedido.FacturaServicios.FirstOrDefault(fs => fs.Id == lineaId);
        if (linea == null)
            return (false, "Línea no encontrada en el pedido.");

        var svc = db.Servicios.FirstOrDefault(s => s.Id == linea.ServicioId);
        if (svc != null && svc.ControlarStock && linea.Cantidad > 0)
        {
            try
            {
                inventarioService.RegistrarEntrada(
                    linea.ServicioId,
                    linea.Cantidad,
                    null,
                    null,
                    null,
                    $"Devolución por quitar línea — pedido {refPedidoLog}",
                    userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inventario al eliminar línea {LineaId} pedido {PedidoId}", lineaId, pedido.Id);
                return (false, $"No se pudo devolver inventario: {ex.Message}");
            }
        }

        if (linea.OpcionesSeleccionadas?.Count > 0)
            db.FacturaServicioOpcionesSeleccion.RemoveRange(linea.OpcionesSeleccionadas);
        db.FacturaServicios.Remove(linea);
        pedido.FacturaServicios.Remove(linea);

        if (pedido.FacturaServicios.Count == 0)
        {
            pedido.Monto = 0;
            pedido.Estado = SD.EstadoOrdenGuardado;
            pedido.EstadoCocina = SD.EstadoCocinaPendiente;
            pedido.MesaId = null;
            pedido.FechaActualizacion = DateTime.Now;
            return (true, null);
        }

        pedido.Monto = Math.Round(
            pedido.FacturaServicios.Sum(fs => fs.Monto),
            2,
            MidpointRounding.AwayFromZero);
        if (pedido.FacturaServicios.Count > 0)
            pedido.ServicioId = pedido.FacturaServicios.First().ServicioId;
        pedido.FechaActualizacion = DateTime.Now;
        return (false, null);
    }
}
