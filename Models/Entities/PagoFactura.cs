using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Tabla intermedia para relacionar un Pago con múltiples Facturas
/// Permite que un pago se aplique a varias facturas simultáneamente
/// </summary>
[Table("PagoFacturas")]
public class PagoFactura
{
    public int Id { get; set; }
    public int PagoId { get; set; }
    public int FacturaId { get; set; }
    public decimal MontoAplicado { get; set; } // Monto de este pago que se aplica a esta factura específica
    
    // Relaciones
    public virtual Pago Pago { get; set; } = null!;
    public virtual Factura Factura { get; set; } = null!;
}

