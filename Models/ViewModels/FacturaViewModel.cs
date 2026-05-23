namespace BarRestPOS.Models.ViewModels;

public class FacturaViewModel
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public int ServicioId { get; set; }
    public string ServicioNombre { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public DateTime MesFacturacion { get; set; }
}

