using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Data;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace BarRestPOS.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public WhatsAppService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public PlantillaMensajeWhatsApp? ObtenerPlantillaDefault()
    {
        try
        {
            return _context.PlantillasMensajeWhatsApp
                .FirstOrDefault(p => p.Activa && p.EsDefault);
        }
        catch (Exception)
        {
            // Si la tabla no existe, retornar null
            return null;
        }
    }

    public PlantillaMensajeWhatsApp? ObtenerPlantilla(int id)
    {
        return _context.PlantillasMensajeWhatsApp
            .FirstOrDefault(p => p.Id == id && p.Activa);
    }

    public string GenerarMensaje(Factura factura, string plantillaMensaje, string? urlBase = null)
    {
        if (string.IsNullOrEmpty(plantillaMensaje))
        {
            return string.Empty;
        }

        var mensaje = plantillaMensaje;
        
        // Generar token seguro para el PDF
        var token = PdfTokenHelper.GenerarToken(factura.Id, _configuration);
        
        // Obtener la URL base para el enlace del PDF (ruta pública con token)
        var enlacePdf = string.IsNullOrEmpty(urlBase) 
            ? $"/facturas/descargar-pdf-publico/{factura.Id}?token={token}"
            : $"{urlBase}/facturas/descargar-pdf-publico/{factura.Id}?token={token}";
        
        // Reemplazar variables
        mensaje = mensaje.Replace("{NombreCliente}", factura.Cliente?.Nombre ?? "");
        mensaje = mensaje.Replace("{CodigoCliente}", factura.Cliente?.Codigo ?? "");
        mensaje = mensaje.Replace("{NumeroFactura}", factura.Numero);
        mensaje = mensaje.Replace("{Monto}", factura.Monto.ToString("N2"));
        mensaje = mensaje.Replace("{Mes}", factura.MesFacturacion.ToString("MMMM yyyy"));
        mensaje = mensaje.Replace("{Categoria}", factura.Categoria);
        mensaje = mensaje.Replace("{Estado}", factura.Estado);
        mensaje = mensaje.Replace("{FechaCreacion}", factura.FechaCreacion.ToString("dd/MM/yyyy"));
        mensaje = mensaje.Replace("{EnlacePDF}", enlacePdf);
        
        return mensaje;
    }

    public string FormatearNumeroWhatsApp(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
        {
            return string.Empty;
        }

        // Remover espacios, guiones, paréntesis y otros caracteres
        var numeroLimpio = Regex.Replace(telefono, @"[^\d]", "");
        
        // Si ya tiene código de país (505), mantenerlo
        if (numeroLimpio.StartsWith("505"))
        {
            return numeroLimpio;
        }
        
        // Si no tiene código de país, agregarlo (Nicaragua: 505)
        if (numeroLimpio.Length >= 8) // Números nicaragüenses tienen 8 dígitos
        {
            return "505" + numeroLimpio;
        }
        
        return numeroLimpio;
    }

    public string GenerarEnlaceWhatsApp(string numeroTelefono, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(numeroTelefono))
        {
            return string.Empty;
        }

        // Codificar el mensaje para URL
        var mensajeCodificado = Uri.EscapeDataString(mensaje);
        
        // Generar enlace de WhatsApp
        return $"https://wa.me/{numeroTelefono}?text={mensajeCodificado}";
    }

    public bool EsNumeroValidoParaWhatsApp(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
        {
            return false;
        }

        // Remover espacios, guiones, paréntesis y otros caracteres
        var numeroLimpio = Regex.Replace(telefono, @"[^\d]", "");
        
        // Validar que tenga al menos 8 dígitos (números nicaragüenses tienen 8 dígitos)
        // Y máximo 15 dígitos (estándar internacional)
        if (numeroLimpio.Length < 8 || numeroLimpio.Length > 15)
        {
            return false;
        }
        
        // Si tiene código de país (505), debe tener 11 dígitos (505 + 8 dígitos)
        if (numeroLimpio.StartsWith("505"))
        {
            return numeroLimpio.Length == 11; // 505 + 8 dígitos
        }
        
        // Si no tiene código de país, debe tener 8 dígitos (formato nicaragüense)
        return numeroLimpio.Length == 8;
    }
}

