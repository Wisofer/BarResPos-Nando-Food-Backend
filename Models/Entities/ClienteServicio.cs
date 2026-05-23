using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

[Table("ClienteProductos")]
public class ClienteServicio
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public int ServicioId { get; set; }
    public int Cantidad { get; set; } = 1; // Cantidad de suscripciones (solo para Streaming)
    public bool Activo { get; set; } = true;
    public DateTime FechaInicio { get; set; } = DateTime.Now;
    public DateTime? FechaFin { get; set; } // Para historial cuando se desactiva
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    
    // Relaciones
    public virtual Cliente Cliente { get; set; } = null!;
    public virtual Servicio Servicio { get; set; } = null!;
}

