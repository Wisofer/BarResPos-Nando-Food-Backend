using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>Opción dentro de un grupo (ej. BBQ, Buffalo).</summary>
[Table("ProductoOpcionItems")]
public class ProductoOpcionItem
{
    public int Id { get; set; }
    public int GrupoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public decimal PrecioAdicional { get; set; }
    public bool Activo { get; set; } = true;

    public virtual ProductoOpcionGrupo Grupo { get; set; } = null!;
}
