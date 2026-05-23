using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class InventarioService : IInventarioService
{
    private readonly ApplicationDbContext _context;

    public InventarioService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<MovimientoInventario> ObtenerTodos()
    {
        return _context.MovimientosInventario
            .Include(m => m.Producto)
            .Include(m => m.Usuario)
            .Include(m => m.Proveedor)
            .OrderByDescending(m => m.Fecha)
            .ToList();
    }

    public List<MovimientoInventario> ObtenerPorProducto(int productoId)
    {
        return _context.MovimientosInventario
            .Include(m => m.Producto)
            .Include(m => m.Usuario)
            .Include(m => m.Proveedor)
            .Where(m => m.ProductoId == productoId)
            .OrderByDescending(m => m.Fecha)
            .ToList();
    }

    public List<MovimientoInventario> ObtenerPorFecha(DateTime fechaInicio, DateTime fechaFin)
    {
        return _context.MovimientosInventario
            .Include(m => m.Producto)
            .Include(m => m.Usuario)
            .Include(m => m.Proveedor)
            .Where(m => m.Fecha >= fechaInicio && m.Fecha <= fechaFin)
            .OrderByDescending(m => m.Fecha)
            .ToList();
    }

    public MovimientoInventario? ObtenerPorId(int id)
    {
        return _context.MovimientosInventario
            .Include(m => m.Producto)
            .Include(m => m.Usuario)
            .Include(m => m.Proveedor)
            .Include(m => m.Factura)
            .FirstOrDefault(m => m.Id == id);
    }

    public MovimientoInventario RegistrarEntrada(int productoId, int cantidad, decimal? costoUnitario, int? proveedorId, string? numeroFactura, string? observaciones, int usuarioId)
    {
        var producto = _context.Servicios.FirstOrDefault(p => p.Id == productoId);
        if (producto == null)
            throw new Exception("Producto no encontrado");

        var stockAnterior = producto.Stock;
        var stockNuevo = stockAnterior + cantidad;
        var costoTotal = costoUnitario.HasValue ? costoUnitario.Value * cantidad : (decimal?)null;

        var movimiento = new MovimientoInventario
        {
            ProductoId = productoId,
            Tipo = SD.TipoMovimientoEntrada,
            Subtipo = SD.SubtipoMovimientoCompra,
            Cantidad = cantidad,
            CostoUnitario = costoUnitario,
            CostoTotal = costoTotal,
            Fecha = DateTime.Now,
            UsuarioId = usuarioId,
            ProveedorId = proveedorId,
            NumeroFactura = numeroFactura,
            Observaciones = observaciones,
            StockAnterior = stockAnterior,
            StockNuevo = stockNuevo
        };

        // Actualizar stock del producto
        producto.Stock = stockNuevo;

        _context.MovimientosInventario.Add(movimiento);
        _context.SaveChanges();

        return movimiento;
    }

    public void AplicarEntradaDevolucionCancelacionSinGuardar(int productoId, int cantidad, int facturaId, int usuarioId, string? observaciones)
    {
        var producto = _context.Servicios.FirstOrDefault(p => p.Id == productoId);
        if (producto == null)
            throw new Exception("Producto no encontrado");
        if (!producto.ControlarStock || cantidad <= 0)
            return;

        var stockAnterior = producto.Stock;
        var stockNuevo = stockAnterior + cantidad;
        producto.Stock = stockNuevo;

        _context.MovimientosInventario.Add(new MovimientoInventario
        {
            ProductoId = productoId,
            Tipo = SD.TipoMovimientoEntrada,
            Subtipo = SD.SubtipoMovimientoDevolucion,
            Cantidad = cantidad,
            Fecha = DateTime.Now,
            UsuarioId = usuarioId,
            FacturaId = facturaId,
            Observaciones = observaciones ?? "Cancelación de pedido",
            StockAnterior = stockAnterior,
            StockNuevo = stockNuevo
        });
    }

    public MovimientoInventario RegistrarSalida(int productoId, int cantidad, string subtipo, int? facturaId, string? observaciones, int usuarioId)
    {
        var producto = _context.Servicios.FirstOrDefault(p => p.Id == productoId);
        if (producto == null)
            throw new Exception("Producto no encontrado");

        // Validar stock disponible si el producto controla stock
        if (producto.ControlarStock && producto.Stock < cantidad)
        {
            throw new Exception($"Stock insuficiente. Disponible: {producto.Stock}, Solicitado: {cantidad}");
        }

        var stockAnterior = producto.Stock;
        var stockNuevo = producto.ControlarStock ? stockAnterior - cantidad : stockAnterior;

        var movimiento = new MovimientoInventario
        {
            ProductoId = productoId,
            Tipo = SD.TipoMovimientoSalida,
            Subtipo = subtipo,
            Cantidad = -cantidad, // Negativo para salidas
            Fecha = DateTime.Now,
            UsuarioId = usuarioId,
            FacturaId = facturaId,
            Observaciones = observaciones,
            StockAnterior = stockAnterior,
            StockNuevo = stockNuevo
        };

        // Actualizar stock del producto solo si controla stock
        if (producto.ControlarStock)
        {
            producto.Stock = stockNuevo;
        }

        _context.MovimientosInventario.Add(movimiento);
        _context.SaveChanges();

        return movimiento;
    }

    public MovimientoInventario RegistrarAjuste(int productoId, int cantidadNueva, string? observaciones, int usuarioId)
    {
        var producto = _context.Servicios.FirstOrDefault(p => p.Id == productoId);
        if (producto == null)
            throw new Exception("Producto no encontrado");

        var stockAnterior = producto.Stock;
        var diferencia = cantidadNueva - stockAnterior;

        var movimiento = new MovimientoInventario
        {
            ProductoId = productoId,
            Tipo = diferencia > 0 ? SD.TipoMovimientoEntrada : SD.TipoMovimientoSalida,
            Subtipo = SD.SubtipoMovimientoAjuste,
            Cantidad = diferencia,
            Fecha = DateTime.Now,
            UsuarioId = usuarioId,
            Observaciones = observaciones ?? "Ajuste de inventario",
            StockAnterior = stockAnterior,
            StockNuevo = cantidadNueva
        };

        // Actualizar stock del producto
        producto.Stock = cantidadNueva;

        _context.MovimientosInventario.Add(movimiento);
        _context.SaveChanges();

        return movimiento;
    }

    public int ObtenerStockActual(int productoId)
    {
        var producto = _context.Servicios.FirstOrDefault(p => p.Id == productoId);
        return producto?.Stock ?? 0;
    }

    public bool ValidarStockDisponible(int productoId, int cantidad)
    {
        var producto = _context.Servicios.FirstOrDefault(p => p.Id == productoId);
        if (producto == null)
            return false;

        // Si no controla stock, siempre hay disponible
        if (!producto.ControlarStock)
            return true;

        return producto.Stock >= cantidad;
    }

    public List<Servicio> ObtenerProductosStockBajo()
    {
        return _context.Servicios
            .Where(p => p.Activo && 
                       p.ControlarStock && 
                       p.StockMinimo > 0 && 
                       p.Stock <= p.StockMinimo)
            .OrderBy(p => p.Stock)
            .ToList();
    }

    public List<MovimientoInventario> ObtenerHistorial(int productoId, int? limite = null)
    {
        var query = _context.MovimientosInventario
            .Include(m => m.Usuario)
            .Include(m => m.Proveedor)
            .Where(m => m.ProductoId == productoId)
            .OrderByDescending(m => m.Fecha);

        if (limite.HasValue)
        {
            return query.Take(limite.Value).ToList();
        }

        return query.ToList();
    }
}

