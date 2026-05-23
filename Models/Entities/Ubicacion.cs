using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

[Table("Ubicaciones")]
public class Ubicacion
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty; // Terraza, Interior, Barra, etc.
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    
    // Relaciones
    public virtual ICollection<Mesa> Mesas { get; set; } = new List<Mesa>();
}

