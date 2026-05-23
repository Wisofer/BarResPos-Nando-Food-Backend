namespace BarRestPOS.Models.ViewModels;

public class PagoViewModel
{
    public int Id { get; set; }
    public int FacturaId { get; set; }
    public string FacturaNumero { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Moneda { get; set; } = string.Empty;
    public string TipoPago { get; set; } = string.Empty;
    public string? Banco { get; set; }
    public string? TipoCuenta { get; set; }
    public decimal? MontoRecibido { get; set; }
    public decimal? Vuelto { get; set; }
    public DateTime FechaPago { get; set; }
}

