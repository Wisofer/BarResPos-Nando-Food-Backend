using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Categoría de productos del restaurante/bar
/// </summary>
[Table("CategoriasProducto")]
public class CategoriaProducto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty; // Bebidas, Comidas, Licores, Postres, Promos
    public string? Descripcion { get; set; }
    /// <summary>Columna legada en BD; no se usa en API ni formularios. Mantiene compatibilidad con despliegues anteriores.</summary>
    public string? ColorHex { get; set; }
    public string? IconoNombre { get; set; } // Nombre del ícono (para UI)
    public int Orden { get; set; } = 0; // Para ordenar en la UI
    /// <summary>
    /// Si es false, los productos de esta categoría no aparecen en KDS ni en ticket de cocina (ej. bebidas).
    /// </summary>
    public bool RequiereCocina { get; set; } = true;
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    
    // Relaciones
    public virtual ICollection<Servicio> Productos { get; set; } = new List<Servicio>();
}

