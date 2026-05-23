using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>Opción elegida en una línea de pedido (con snapshot para impresión e historial).</summary>
[Table("OrdenLineaOpciones")]
public class FacturaServicioOpcionSeleccion
{
    public int Id { get; set; }
    public int FacturaServicioId { get; set; }
    public int ProductoOpcionGrupoId { get; set; }
    public int ProductoOpcionItemId { get; set; }
    public string NombreGrupo { get; set; } = string.Empty;
    public string NombreOpcion { get; set; } = string.Empty;
    public decimal PrecioAdicional { get; set; }

    public virtual FacturaServicio FacturaServicio { get; set; } = null!;
}
