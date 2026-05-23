using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface IPagoService
{
    List<Pago> ObtenerTodos();
    Pago? ObtenerPorId(int id);
    List<Pago> ObtenerPorFactura(int facturaId);
    decimal CalcularVuelto(decimal montoRecibido, decimal costo);
    decimal ConvertirMoneda(decimal monto, string monedaOrigen, string monedaDestino);
    decimal CalcularMontoTotal(Pago pago);
    Pago Crear(Pago pago, List<int>? facturaIds = null, List<decimal>? montosAplicados = null);
    Pago Actualizar(Pago pago);
    bool Eliminar(int id);
    (int eliminados, int noEncontrados) EliminarMultiples(List<int> ids);
    decimal CalcularTotalIngresos();
}

