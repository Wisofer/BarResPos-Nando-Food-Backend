using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

[Table("Pagos")]
public class Pago
{
    public int Id { get; set; }
    public int? FacturaId { get; set; } // Nullable para permitir pagos con múltiples facturas
    public decimal Monto { get; set; }
    public string Moneda { get; set; } = "C$"; // C$, $, Ambos
    public string TipoPago { get; set; } = string.Empty; // Fisico, Electronico, Mixto
    public string? Banco { get; set; } // Banpro, Lafise, BAC, Ficohsa, BDF
    public string? TipoCuenta { get; set; } // Cuenta $, Cuenta C$, Billetera movil
    public decimal? MontoRecibido { get; set; } // Para pago físico (compatibilidad)
    public decimal? Vuelto { get; set; } // Para pago físico (compatibilidad)
    public decimal? TipoCambio { get; set; } // Tipo de cambio usado (C$36.80 = $1)
    
    // Campos para pago físico con múltiples monedas
    public decimal? MontoCordobasFisico { get; set; }
    public decimal? MontoDolaresFisico { get; set; }
    public decimal? MontoRecibidoFisico { get; set; }
    public decimal? VueltoFisico { get; set; }
    
    // Campos para pago electrónico con múltiples monedas
    public decimal? MontoCordobasElectronico { get; set; }
    public decimal? MontoDolaresElectronico { get; set; }
    public DateTime FechaPago { get; set; } = DateTime.Now;
    public string? Observaciones { get; set; }

    /// <summary>Descuento aplicado al cobrar, en córdobas (no altera el subtotal de líneas del pedido).</summary>
    public decimal DescuentoMonto { get; set; }

    /// <summary>Motivo o nota del descuento (auditoría).</summary>
    public string? DescuentoMotivo { get; set; }
    
    // Relaciones
    public virtual Factura Factura { get; set; } = null!; // Para compatibilidad con pagos de una sola factura
    public virtual ICollection<PagoFactura> PagoFacturas { get; set; } = new List<PagoFactura>(); // Para pagos con múltiples facturas
}

