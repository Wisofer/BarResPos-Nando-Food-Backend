namespace BarRestPOS.Services.IServices;

public interface IDashboardService
{
    Task<object> ObtenerResumenAsync(DateTime? desde, DateTime? hasta, int topProductos);
}
