using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Producto incluido en una orden
/// </summary>
[Table("OrdenProductos")]
public class FacturaServicio
{
    public int Id { get; set; }
    public int FacturaId { get; set; } // Orden
    public int ServicioId { get; set; } // Producto
    public int Cantidad { get; set; } = 1; // Cantidad de productos
    public decimal PrecioUnitario { get; set; } // Precio al momento de la venta
    public decimal Monto { get; set; } // Subtotal (PrecioUnitario * Cantidad)
    public string? Notas { get; set; } // Notas especiales: "sin hielo", "extra queso", "término medio"
    public string Estado { get; set; } = "Pendiente"; // Pendiente, En Preparación, Listo, Entregado
    public DateTime? FechaEnvioCocina { get; set; } // Fecha/hora en que fue enviado a cocina/bar
    
    // Propiedades de navegación
    public virtual Factura Factura { get; set; } = null!;
    public virtual Servicio Servicio { get; set; } = null!;
    public virtual ICollection<FacturaServicioOpcionSeleccion> OpcionesSeleccionadas { get; set; } = new List<FacturaServicioOpcionSeleccion>();
}

