using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

/// <summary>
/// Servicio para generar tickets de impresión
/// </summary>
public interface IImpresionService
{
    /// <summary>
    /// Genera los bytes ESC/POS del ticket de cocina
    /// </summary>
    byte[] GenerarTicketCocina(Factura orden, System.Collections.Generic.List<int>? lineasFilter = null);

    /// <summary>
    /// Genera los bytes ESC/POS del ticket de bar
    /// </summary>
    byte[] GenerarTicketBar(Factura orden, System.Collections.Generic.List<int>? lineasFilter = null);
    
    /// <summary>
    /// Genera los bytes ESC/POS del ticket de recibo/pago
    /// </summary>
    byte[] GenerarTicketRecibo(Pago pago, Factura orden);
    
    /// <summary>
    /// Genera los bytes ESC/POS del ticket de comanda para mesero
    /// </summary>
    byte[] GenerarTicketComanda(Factura orden);

    /// <summary>
    /// Genera texto plano estructurado del ticket de recibo/pago para previsualización
    /// </summary>
    string GenerarPreviewRecibo(Pago pago, Factura orden);

    /// <summary>
    /// Genera texto plano estructurado del ticket de comanda para previsualización
    /// </summary>
    string GenerarPreviewComanda(Factura orden);

    /// <summary>
    /// Genera texto plano estructurado del ticket de cocina para previsualización
    /// </summary>
    string GenerarPreviewCocina(Factura orden, System.Collections.Generic.List<int>? lineasFilter = null);

    /// <summary>
    /// Genera texto plano estructurado del ticket de bar para previsualización
    /// </summary>
    string GenerarPreviewBar(Factura orden, System.Collections.Generic.List<int>? lineasFilter = null);
}

