using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Representa un movimiento de inventario (entrada, salida, ajuste)
/// </summary>
[Table("MovimientosInventario")]
public class MovimientoInventario
{
    public int Id { get; set; }
    
    /// <summary>
    /// Producto relacionado
    /// </summary>
    public int ProductoId { get; set; }
    
    /// <summary>
    /// Tipo de movimiento: "Entrada" o "Salida"
    /// </summary>
    public string Tipo { get; set; } = string.Empty; // Entrada, Salida
    
    /// <summary>
    /// Subtipo de movimiento: Compra, Venta, Daño, Ajuste, Transferencia, etc.
    /// </summary>
    public string Subtipo { get; set; } = string.Empty; // Compra, Venta, Daño, Ajuste, Transferencia, Devolución
    
    /// <summary>
    /// Cantidad del movimiento (positiva para entradas, negativa para salidas)
    /// </summary>
    public int Cantidad { get; set; }
    
    /// <summary>
    /// Costo unitario (principalmente para compras)
    /// </summary>
    public decimal? CostoUnitario { get; set; }
    
    /// <summary>
    /// Costo total del movimiento
    /// </summary>
    public decimal? CostoTotal { get; set; }
    
    /// <summary>
    /// Fecha del movimiento
    /// </summary>
    public DateTime Fecha { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Usuario que realizó el movimiento
    /// </summary>
    public int UsuarioId { get; set; }
    
    /// <summary>
    /// Proveedor (si es una compra)
    /// </summary>
    public int? ProveedorId { get; set; }
    
    /// <summary>
    /// Número de factura o documento (para compras)
    /// </summary>
    public string? NumeroFactura { get; set; }
    
    /// <summary>
    /// Orden relacionada (si es una venta)
    /// </summary>
    public int? FacturaId { get; set; }
    
    /// <summary>
    /// Observaciones o notas del movimiento
    /// </summary>
    public string? Observaciones { get; set; }
    
    /// <summary>
    /// Stock anterior antes del movimiento
    /// </summary>
    public int StockAnterior { get; set; }
    
    /// <summary>
    /// Stock después del movimiento
    /// </summary>
    public int StockNuevo { get; set; }
    
    // Relaciones
    public virtual Servicio Producto { get; set; } = null!;
    public virtual Usuario Usuario { get; set; } = null!;
    public virtual Proveedor? Proveedor { get; set; }
    public virtual Factura? Factura { get; set; }
}

