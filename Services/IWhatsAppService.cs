using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface IWhatsAppService
{
    /// <summary>
    /// Obtiene la plantilla activa por defecto
    /// </summary>
    PlantillaMensajeWhatsApp? ObtenerPlantillaDefault();
    
    /// <summary>
    /// Obtiene una plantilla por ID
    /// </summary>
    PlantillaMensajeWhatsApp? ObtenerPlantilla(int id);
    
    /// <summary>
    /// Reemplaza las variables en el mensaje con los datos de la factura
    /// </summary>
    string GenerarMensaje(Factura factura, string plantillaMensaje, string? urlBase = null);
    
    /// <summary>
    /// Formatea el número de teléfono para WhatsApp (código de país + número)
    /// </summary>
    string FormatearNumeroWhatsApp(string? telefono);
    
    /// <summary>
    /// Genera el enlace de WhatsApp con el mensaje prellenado
    /// </summary>
    string GenerarEnlaceWhatsApp(string numeroTelefono, string mensaje);
    
    /// <summary>
    /// Valida si un número de teléfono es válido para WhatsApp
    /// </summary>
    bool EsNumeroValidoParaWhatsApp(string? telefono);
}

