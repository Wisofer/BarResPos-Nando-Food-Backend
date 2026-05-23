namespace BarRestPOS.Models.ViewModels;

public class DashboardViewModel
{
    // ========== MÉTRICAS PRINCIPALES DEL RESTAURANTE ==========
    public decimal VentasDelDia { get; set; }
    public decimal VentasDeLaSemana { get; set; }
    public decimal VentasDelMes { get; set; }
    public decimal PromedioTicket { get; set; }
    
    // ========== CONTADORES GENERALES ==========
    public int OrdenesDelDia { get; set; }
    public int OrdenesPendientes { get; set; }
    public int OrdenesCompletadas { get; set; }
    public int ClientesAtendidos { get; set; }
    
    // ========== ESTADO DE MESAS ==========
    public int TotalMesas { get; set; }
    public int MesasLibres { get; set; }
    public int MesasOcupadas { get; set; }
    public int MesasReservadas { get; set; }
    
    // ========== VENTAS POR CATEGORÍA (compatibilidad) ==========
    public decimal VentasBebidas { get; set; }
    public decimal VentasComidas { get; set; }
    public decimal VentasLicores { get; set; }
    public decimal VentasPostres { get; set; }
    public decimal VentasCocteles { get; set; }
    
    public int OrdenesBebidas { get; set; }
    public int OrdenesComidas { get; set; }
    public int OrdenesLicores { get; set; }
    public int OrdenesPostres { get; set; }
    public int OrdenesCocteles { get; set; }
    
    // ========== VENTAS POR CATEGORÍA REAL (dinámico) ==========
    public List<VentaPorCategoria> VentasPorCategoria { get; set; } = new();
    
    // ========== PRODUCTOS MÁS VENDIDOS ==========
    public List<ProductoMasVendido> ProductosMasVendidos { get; set; } = new();
    
    // ========== ESTADÍSTICAS DE COCINA ==========
    public int OrdenesEnCocina { get; set; }
    public int OrdenesListas { get; set; }
    public double TiempoPromedioPreparacion { get; set; }
    
    // ========== ESTADÍSTICAS POR HORA ==========
    public List<VentaPorHora> VentasPorHora { get; set; } = new();
    
    // ========== DATOS PARA GRÁFICOS ==========
    public List<VentaDiaria> VentasUltimosDias { get; set; } = new();
    
    // ========== INVENTARIO ==========
    public int TotalProductos { get; set; }
    public int ProductosConStock { get; set; }
    public int ProductosStockBajo { get; set; }
    public decimal ValorInventario { get; set; }
    public List<ProductoStockBajo> ProductosStockBajoLista { get; set; } = new();
    
    // ========== CAJA ==========
    public bool CajaAbierta { get; set; }
    public decimal MontoInicialCaja { get; set; }
    public decimal TotalCajaHoy { get; set; }
    public int OrdenesPendientesPago { get; set; }
    
    // ========== COMPATIBILIDAD (mantener para no romper código antiguo) ==========
    public decimal PagosPendientes { get; set; }
    public decimal PagosRealizados { get; set; }
    public int TotalClientes { get; set; }
    public int TotalFacturas { get; set; }
}

// ========== CLASES AUXILIARES ==========
public class ProductoMasVendido
{
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Total { get; set; }
}

public class VentaPorHora
{
    public int Hora { get; set; }
    public decimal Monto { get; set; }
    public int Ordenes { get; set; }
}

public class VentaDiaria
{
    public string Fecha { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public int Ordenes { get; set; }
}

// ========== NUEVAS CLASES PARA DASHBOARD MEJORADO ==========
public class VentaPorCategoria
{
    public string NombreCategoria { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Cantidad { get; set; }
    public string ColorHex { get; set; } = "#3B82F6";
    public string Icono { get; set; } = "📦";
}

public class ProductoStockBajo
{
    public string Nombre { get; set; } = string.Empty;
    public int Stock { get; set; }
    public int StockMinimo { get; set; }
}

// Mantener por compatibilidad
public class MesEstadistica
{
    public string Mes { get; set; } = "";
    public decimal IngresosInternet { get; set; }
    public decimal IngresosStreaming { get; set; }
    public int FacturasInternet { get; set; }
    public int FacturasStreaming { get; set; }
}
