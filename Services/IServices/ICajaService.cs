using BarRestPOS.Models.Api;
using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface ICajaService
{
    Task<EstadoCajaResponse> ObtenerEstadoActualAsync();
    Task<List<object>> ObtenerOrdenesPendientesAsync();
    Task<CierreCaja> AbrirCajaAsync(decimal montoInicial, int usuarioId);
    Task<PreviewCierreCajaResponse> ObtenerPreviewCierreAsync();
    Task<CierreCaja> CerrarCajaAsync(decimal? montoReal, string? observaciones);
    Task<PagedResult<CierreCaja>> ObtenerHistorialAsync(int page, int pageSize, DateTime? desde = null, DateTime? hasta = null);
    Task<List<CierreCaja>> ObtenerHistorialParaExportAsync(DateTime? desde, DateTime? hasta);
    Task<CierreCaja?> ObtenerCierrePorIdAsync(int id);
    Task<List<object>> ObtenerPagosPorFechaCierreAsync(DateTime fechaCierre);
}

public class EstadoCajaResponse
{
    public bool Abierta { get; set; }
    public CierreCaja? Cierre { get; set; }
}

public class PreviewCierreCajaResponse
{
    public int CierreId { get; set; }
    public DateTime FechaCierre { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal MontoInicial { get; set; }
    public decimal TotalVentasNetas { get; set; }
    public decimal TotalEfectivo { get; set; }
    public decimal TotalTarjeta { get; set; }
    public decimal TotalTransferencia { get; set; }
    public decimal TotalCordobas { get; set; }
    public decimal TotalDolares { get; set; }
    public decimal TotalGeneral { get; set; }
    public int TotalOrdenes { get; set; }
    public int TotalPagos { get; set; }
    public decimal MontoEsperado { get; set; }
}
