using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface IInventarioService
{
    // Obtener movimientos
    List<MovimientoInventario> ObtenerTodos();
    List<MovimientoInventario> ObtenerPorProducto(int productoId);
    List<MovimientoInventario> ObtenerPorFecha(DateTime fechaInicio, DateTime fechaFin);
    MovimientoInventario? ObtenerPorId(int id);
    
    // Crear movimientos
    MovimientoInventario RegistrarEntrada(int productoId, int cantidad, decimal? costoUnitario, int? proveedorId, string? numeroFactura, string? observaciones, int usuarioId);
    MovimientoInventario RegistrarSalida(int productoId, int cantidad, string subtipo, int? facturaId, string? observaciones, int usuarioId);
    MovimientoInventario RegistrarAjuste(int productoId, int cantidadNueva, string? observaciones, int usuarioId);

    /// <summary>Entrada por cancelación de pedido; no llama SaveChanges (el llamador hace commit único).</summary>
    void AplicarEntradaDevolucionCancelacionSinGuardar(int productoId, int cantidad, int facturaId, int usuarioId, string? observaciones);
    
    // Obtener stock actual
    int ObtenerStockActual(int productoId);
    
    // Validar stock disponible
    bool ValidarStockDisponible(int productoId, int cantidad);
    
    // Obtener productos con stock bajo
    List<Servicio> ObtenerProductosStockBajo();
    
    // Historial de movimientos
    List<MovimientoInventario> ObtenerHistorial(int productoId, int? limite = null);
}

