using System.Text.Json.Serialization;

namespace BarRestPOS.Services.IServices;

public interface IReporteService
{
    Task<ResumenVentasResponse> ObtenerResumenVentasAsync(DateTime? desde, DateTime? hasta);
    Task<List<VentaDetalleReporte>> ObtenerDetalleVentasAsync(DateTime? desde, DateTime? hasta, string? filtroVentas = "activas");
    Task<VentaTicketCompletoReporte?> ObtenerTicketCompletoPorOrdenIdAsync(int ordenId);
    Task<List<VentaPorCategoriaReporte>> ObtenerVentasPorCategoriaAsync(DateTime? desde, DateTime? hasta);
    Task<List<VentaPorCategoriaConDesgloseReporte>> ObtenerVentasPorCategoriaConDesgloseAsync(DateTime? desde, DateTime? hasta);
    Task<List<ProductoTopReporte>> ObtenerProductosTopAsync(DateTime? desde, DateTime? hasta, int top);
    Task<List<VentaPorMeseroReporte>> ObtenerVentasPorMeseroAsync(DateTime? desde, DateTime? hasta);

    byte[] GenerarExcelVentas(DateTime desde, DateTime hasta, List<VentaDetalleReporte> ventas);
    byte[] GenerarExcelCategorias(DateTime desde, DateTime hasta, List<VentaPorCategoriaReporte> items);
    byte[] GenerarExcelCategoriasConDesglose(DateTime desde, DateTime hasta, List<VentaPorCategoriaConDesgloseReporte> items);
    byte[] GenerarExcelTopProductos(DateTime desde, DateTime hasta, List<ProductoTopReporte> items);
    byte[] GenerarExcelVentasPorMesero(DateTime desde, DateTime hasta, List<VentaPorMeseroReporte> items);
}

public class ResumenVentasResponse
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public decimal TotalVentas { get; set; }
    public int TotalOrdenes { get; set; }
    public decimal PromedioTicket { get; set; }
    public List<VentaPorDiaReporte> PorDia { get; set; } = new();
}

public class VentaPorDiaReporte
{
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public int Ordenes { get; set; }
}

public class VentaDetalleReporte
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public string? Cliente { get; set; }
    public string? Mesero { get; set; }
    public decimal SubtotalLineas { get; set; }
    public int CantidadLineas { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaUltimaActualizacion { get; set; }
    public string MetodoPago { get; set; } = "";
    public string? Moneda { get; set; }
}

public class VentaTicketCompletoReporte
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string? Cliente { get; set; }
    public string? Mesero { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal SubtotalLineas { get; set; }
    public decimal TotalCobrado { get; set; }
    public int CantidadLineas { get; set; }
    public int CantidadUnidades { get; set; }
    public string MetodoPago { get; set; } = "";
    public string? Moneda { get; set; }
    public List<VentaLineaReporte> Lineas { get; set; } = new();
}

public class VentaLineaReporte
{
    public int DetalleId { get; set; }
    public bool Anulado { get; set; }
    public int ProductoId { get; set; }
    [JsonPropertyName("codigoProducto")]
    public string CodigoProducto { get; set; } = "";
    [JsonPropertyName("productoNombre")]
    public string NombreProducto { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal TotalLinea { get; set; }
    public string? Notas { get; set; }
}

public class VentaPorCategoriaReporte
{
    public string Categoria { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public int Cantidad { get; set; }
}

public class VentaPorCategoriaConDesgloseReporte
{
    public string Categoria { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public int Cantidad { get; set; }
    public List<VentaPorCategoriaProductoDesglose> Productos { get; set; } = new();
}

public class VentaPorCategoriaProductoDesglose
{
    public int ProductoId { get; set; }
    [JsonPropertyName("codigoProducto")]
    public string CodigoProducto { get; set; } = "";
    [JsonPropertyName("productoNombre")]
    public string NombreProducto { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal Monto { get; set; }
}

public class ProductoTopReporte
{
    public int ProductoId { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Venta { get; set; }
    public List<ProductoTopDesglosePago> DesglosePorFormaPago { get; set; } = new();
}

public class ProductoTopDesglosePago
{
    public string MetodoPago { get; set; } = "";
    public string? Moneda { get; set; }
    public int CantidadUnidades { get; set; }
    public decimal MontoCordobas { get; set; }
}

public class VentaPorMeseroReporte
{
    public int? MeseroId { get; set; }
    public string Mesero { get; set; } = "";
    public int CantidadOrdenes { get; set; }
    public decimal TotalNeto { get; set; }
    public decimal PromedioTicket { get; set; }
}
