using BarRestPOS.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BarRestPOS.Data;

public static class InicializarPlantillaWhatsApp
{
    public static void CrearPlantillaDefaultSiNoExiste(ApplicationDbContext context, ILogger logger)
    {
        try
        {
            // Verificar si ya existe una plantilla por defecto
            if (context.PlantillasMensajeWhatsApp.Any(p => p.EsDefault))
            {
                return;
            }

            logger.LogInformation("Creando plantilla por defecto de WhatsApp...");

            var plantillaDefault = new PlantillaMensajeWhatsApp
            {
                Nombre = "Plantilla por Defecto",
                Mensaje = "Hola {NombreCliente},\n\n" +
                         "Le enviamos su factura del mes {Mes}.\n\n" +
                         "🔗 Descargar PDF:\n{EnlacePDF}\n\n" +
                         "Gracias por su preferencia.",
                Activa = true,
                EsDefault = true,
                FechaCreacion = DateTime.Now
            };

            context.PlantillasMensajeWhatsApp.Add(plantillaDefault);
            context.SaveChanges();

            logger.LogInformation("Plantilla por defecto de WhatsApp creada correctamente.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al crear la plantilla por defecto de WhatsApp.");
        }
    }
}

