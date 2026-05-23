using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

/// <summary>
/// Servicio para generar tickets de impresión
/// </summary>
public interface IImpresionService
{
    /// <summary>
    /// Genera el HTML del ticket de cocina
    /// </summary>
    string GenerarTicketCocina(Factura orden);
    
    /// <summary>
    /// Genera el HTML del ticket de recibo/pago
    /// </summary>
    string GenerarTicketRecibo(Pago pago, Factura orden);
    
    /// <summary>
    /// Genera el HTML del ticket de comanda para mesero
    /// </summary>
    string GenerarTicketComanda(Factura orden);
}

