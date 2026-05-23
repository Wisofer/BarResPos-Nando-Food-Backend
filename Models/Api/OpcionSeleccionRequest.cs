namespace BarRestPOS.Models.Api;

/// <summary>Selección de opción en una línea (POS / pedido).</summary>
public class OpcionSeleccionRequest
{
    public int GrupoId { get; set; }
    public int OpcionId { get; set; }
}
