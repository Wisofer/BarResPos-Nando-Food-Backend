using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface IFacturaService
{
    List<Factura> ObtenerTodas();
    Factura? ObtenerPorId(int id);
    List<Factura> ObtenerPorCliente(int clienteId);
    List<Factura> ObtenerPorMes(DateTime mes);
    List<Factura> ObtenerPendientes();
    string GenerarNumeroFactura(Cliente cliente, DateTime mes, string? categoria = null);
    Factura Crear(Factura factura);
    List<Factura> CrearFacturasAgrupadasPorCategoria(int clienteId, List<int> servicioIds, DateTime mesFacturacion);
    Factura Actualizar(Factura factura);
    bool Eliminar(int id);
    (int eliminadas, int conPagos, int noEncontradas) EliminarMultiples(List<int> ids);
    void GenerarFacturasAutomaticas();
    decimal CalcularTotalPendiente();
    decimal CalcularTotalPagado();
    int ObtenerTotal();
    int ObtenerTotalPagadas();
    int ObtenerTotalPendientes();
    decimal ObtenerMontoTotal();
}

