using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Representa un proveedor de productos
/// </summary>
[Table("Proveedores")]
public class Proveedor
{
    public int Id { get; set; }
    
    /// <summary>
    /// Nombre del proveedor
    /// </summary>
    public string Nombre { get; set; } = string.Empty;
    
    /// <summary>
    /// Teléfono de contacto
    /// </summary>
    public string? Telefono { get; set; }
    
    /// <summary>
    /// Email de contacto
    /// </summary>
    public string? Email { get; set; }
    
    /// <summary>
    /// Dirección del proveedor
    /// </summary>
    public string? Direccion { get; set; }
    
    /// <summary>
    /// Contacto principal
    /// </summary>
    public string? Contacto { get; set; }
    
    /// <summary>
    /// Observaciones o notas
    /// </summary>
    public string? Observaciones { get; set; }
    
    /// <summary>
    /// Si el proveedor está activo
    /// </summary>
    public bool Activo { get; set; } = true;
    
    /// <summary>
    /// Fecha de creación
    /// </summary>
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    
    // Relaciones
    public virtual ICollection<MovimientoInventario> MovimientosInventario { get; set; } = new List<MovimientoInventario>();
}

