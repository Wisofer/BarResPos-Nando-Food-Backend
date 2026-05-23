using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Representa una mesa del restaurante/bar
/// </summary>
[Table("Mesas")]
public class Mesa
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty; // "M1", "M2", "Terraza-1", etc.
    public int Capacidad { get; set; } = 4; // Número de personas
    public string Estado { get; set; } = "Libre"; // Libre, Ocupada, Reservada
    public int? UbicacionId { get; set; } // Salón, Terraza, VIP, Bar
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    
    // Relaciones
    public virtual Ubicacion? Ubicacion { get; set; }
    public virtual ICollection<Factura> Ordenes { get; set; } = new List<Factura>(); // Órdenes de esta mesa
}

