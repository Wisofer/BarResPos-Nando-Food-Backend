using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

[Table("PlantillasMensajeWhatsApp")]
public class PlantillaMensajeWhatsApp
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty; // Nombre descriptivo de la plantilla
    public string Mensaje { get; set; } = string.Empty; // Plantilla del mensaje con variables
    public bool Activa { get; set; } = true; // Si está activa o no
    public bool EsDefault { get; set; } = false; // Si es la plantilla por defecto
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? FechaActualizacion { get; set; }
    
    // Variables disponibles en el mensaje:
    // {NombreCliente} - Nombre del cliente
    // {CodigoCliente} - Código del cliente
    // {NumeroFactura} - Número de factura
    // {Monto} - Monto de la factura
    // {Mes} - Mes de facturación
    // {Categoria} - Categoría (Internet/Streaming)
    // {Estado} - Estado de la factura
    // {FechaCreacion} - Fecha de creación
    // {EnlacePDF} - Enlace para descargar el PDF
}

