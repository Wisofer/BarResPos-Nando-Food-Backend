using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>Grupo de opciones configurable para un producto (ej. Salsa, Nivel de picante).</summary>
[Table("ProductoOpcionGrupos")]
public class ProductoOpcionGrupo
{
    public int Id { get; set; }
    public int ServicioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Obligatorio { get; set; }
    /// <summary>Mínimo de opciones a elegir en este grupo.</summary>
    public int MinSeleccion { get; set; }
    /// <summary>Máximo de opciones; 0 = sin límite superior.</summary>
    public int MaxSeleccion { get; set; }
    public bool ReemplazaPrecioBase { get; set; } = false;
    public bool Activo { get; set; } = true;

    public virtual Servicio Servicio { get; set; } = null!;
    public virtual ICollection<ProductoOpcionItem> Opciones { get; set; } = new List<ProductoOpcionItem>();
}
