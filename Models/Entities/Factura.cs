using System.ComponentModel.DataAnnotations.Schema;
using BarRestPOS.Utils;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Orden de compra del restaurante/bar
/// </summary>
[Table("Ordenes")]
public class Factura
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty; // # de orden: "ORD-001"
    
    // Información de la mesa y mesero
    public int? MesaId { get; set; } // Mesa del restaurante
    public int? ClienteId { get; set; } // Cliente (opcional, para clientes frecuentes o "Cliente General")
    public int? MeseroId { get; set; } // Usuario que tomó la orden (rol Mesero)
    
    // Información del pedido
    public int ServicioId { get; set; } // Servicio principal (para compatibilidad, se puede eliminar después)
    public string Categoria { get; set; } = "General"; // General, Bebidas, Comidas, etc.
    /// <summary>Salon (mesas/POS salón) o Delivery (pedido a domicilio).</summary>
    public string OrigenPedido { get; set; } = SD.OrigenPedidoSalon;
    public string? DeliveryClienteNombre { get; set; }
    public string? DeliveryClienteTelefono { get; set; }
    public string? DeliveryClienteDireccion { get; set; }
    public decimal Monto { get; set; }
    
    // Estados
    public string Estado { get; set; } = "Pendiente"; // Pendiente, En Cocina, Listo, Servido, Pagado, Cancelado
    public string EstadoCocina { get; set; } = "Pendiente"; // Pendiente, En Preparación, Listo, Entregado
    
    // Tiempos
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? FechaActualizacion { get; set; }
    public DateTime? FechaEnvioCocina { get; set; } // Cuando se envió a cocina
    public DateTime? FechaListo { get; set; } // Cuando la cocina marcó como listo
    public DateTime? FechaServido { get; set; } // Cuando el mesero lo sirvió
    public DateTime? FechaPagado { get; set; } // Cuando se pagó
    public int TiempoPreparacion { get; set; } = 0; // Minutos de preparación
    
    // Compatibilidad con sistema antiguo
    public DateTime MesFacturacion { get; set; } = DateTime.Now; // Mantener por compatibilidad
    public string? ArchivoPDF { get; set; } // Ruta del archivo PDF
    
    // Observaciones
    public string? Observaciones { get; set; } // Notas especiales de la orden
    
    // Relaciones
    public virtual Mesa? Mesa { get; set; }
    public virtual Cliente? Cliente { get; set; }
    public virtual Usuario? Mesero { get; set; } // Usuario que tomó la orden
    public virtual Servicio Servicio { get; set; } = null!; // Servicio principal (para compatibilidad)
    public virtual ICollection<FacturaServicio> FacturaServicios { get; set; } = new List<FacturaServicio>(); // Productos incluidos en esta orden
    public virtual ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}

