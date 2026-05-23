using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

[Table("Clientes")]
public class Cliente
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Cedula { get; set; }
    public string? Email { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public int TotalFacturas { get; set; } = 0; // Contador de facturas del cliente
    public int? ServicioId { get; set; } // Último servicio usado por el cliente (mantener para compatibilidad)
    
    // Relaciones
    public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();
    public virtual ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public virtual Servicio? Servicio { get; set; } // Relación con el último servicio usado (mantener para compatibilidad)
    public virtual ICollection<ClienteServicio> ClienteServicios { get; set; } = new List<ClienteServicio>(); // Relación muchos-a-muchos
}

