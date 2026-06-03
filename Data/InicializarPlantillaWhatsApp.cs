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
            var plantillaDefault = context.PlantillasMensajeWhatsApp.FirstOrDefault(p => p.EsDefault);
            if (plantillaDefault != null)
            {
                // Si la plantilla por defecto es la antigua (contiene Mes o EnlacePDF) o no tiene la nueva variable NombreRestaurante, la actualizamos
                if (plantillaDefault.Mensaje.Contains("{Mes}") || plantillaDefault.Mensaje.Contains("{EnlacePDF}") || !plantillaDefault.Mensaje.Contains("{NombreRestaurante}"))
                {
                    logger.LogInformation("Actualizando plantilla de WhatsApp heredada a la nueva plantilla por defecto con NombreRestaurante...");
                    plantillaDefault.Mensaje = "*{NombreRestaurante} - TICKET DIGITAL*\n" +
                                              "==========================================\n" +
                                              "*Cliente:* {NombreCliente}\n" +
                                              "*Pedido:* {CodigoPedido}\n" +
                                              "*Fecha:* {Fecha}\n\n" +
                                              "*Detalle de tu Compra:*\n" +
                                              "{DetallePedido}" +
                                              "==========================================\n" +
                                              "*Subtotal:* {Subtotal}\n" +
                                              "{Descuento}" +
                                              "*Total a Pagar:* {Total}\n" +
                                              "*Método de Pago:* {MetodoPago}\n" +
                                              "==========================================\n" +
                                              "¡Muchas gracias por su preferencia!";
                    plantillaDefault.FechaActualizacion = DateTime.Now;
                    context.SaveChanges();
                    logger.LogInformation("Plantilla de WhatsApp heredada actualizada correctamente.");
                }
                return;
            }

            logger.LogInformation("Creando plantilla por defecto de WhatsApp...");

            var nuevaPlantillaDefault = new PlantillaMensajeWhatsApp
            {
                Nombre = "Plantilla por Defecto",
                Mensaje = "*{NombreRestaurante} - TICKET DIGITAL*\n" +
                          "==========================================\n" +
                          "*Cliente:* {NombreCliente}\n" +
                          "*Pedido:* {CodigoPedido}\n" +
                          "*Fecha:* {Fecha}\n\n" +
                          "*Detalle de tu Compra:*\n" +
                          "{DetallePedido}" +
                          "==========================================\n" +
                          "*Subtotal:* {Subtotal}\n" +
                          "{Descuento}" +
                          "*Total a Pagar:* {Total}\n" +
                          "*Método de Pago:* {MetodoPago}\n" +
                          "==========================================\n" +
                          "¡Muchas gracias por su preferencia!",
                Activa = true,
                EsDefault = true,
                FechaCreacion = DateTime.Now
            };

            context.PlantillasMensajeWhatsApp.Add(nuevaPlantillaDefault);
            context.SaveChanges();

            logger.LogInformation("Plantilla por defecto de WhatsApp creada correctamente.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al crear la plantilla por defecto de WhatsApp.");
        }
    }
}

