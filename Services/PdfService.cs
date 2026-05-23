using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using BarRestPOS.Models.Entities;
using BarRestPOS.Utils;
using BarRestPOS.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace BarRestPOS.Services;

public class PdfService : IPdfService
{
    private readonly IWebHostEnvironment? _environment;
    private readonly ApplicationDbContext? _context;

    public PdfService(IWebHostEnvironment? environment = null, ApplicationDbContext? context = null)
    {
        // La licencia de QuestPDF se inicializa en Program.cs
        _environment = environment;
        _context = context;
    }

    /// <summary>
    /// Genera un PDF simplificado para facturas de Streaming (sin proporcional, sin multa)
    /// </summary>
    public byte[] GenerarPdfFacturaStreaming(Factura factura)
    {
        var fechaEmision = DateTime.Now;
        var mesFacturado = factura.MesFacturacion.ToString("MMM/yyyy").ToUpper();
        
        // Cargar logo si existe
        var logoBytes = CargarLogo();
        
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9));

                // Header simplificado
                page.Header()
                    .Column(column =>
                    {
                        column.Item().Row(row =>
                        {
                            // Logo (si existe)
                            if (logoBytes != null)
                            {
                                row.ConstantItem(80).Height(60).AlignLeft().AlignMiddle()
                                    .Image(logoBytes).FitArea();
                            }
                            else
                            {
                                row.ConstantItem(80);
                            }
                            
                            // Información de la Empresa
                            row.RelativeItem().PaddingLeft(10).Column(empresaColumn =>
                            {
                                empresaColumn.Item().Text("Servicios de Streaming").FontSize(16).Bold().FontColor(Colors.Purple.Darken3);
                                empresaColumn.Item().Text("Sistema de Facturación").FontSize(9).FontColor(Colors.Grey.Darken2);
                                empresaColumn.Item().Text("Nicaragua").FontSize(8).FontColor(Colors.Grey.Medium);
                                empresaColumn.Item().Text("Correo: atencion.al.cliente@emsinetsolut.com").FontSize(8).FontColor(Colors.Grey.Medium);
                                empresaColumn.Item().Text("Teléfonos: 89308058 / 82771485").FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                        });

                        column.Item().PaddingTop(10).Row(row =>
                        {
                            // Número de Factura destacado - Formato F-0001-STR
                            row.RelativeItem().Column(facturaColumn =>
                            {
                                // Extraer el número del formato "0001-Nombre-MMYYYY-STR" y mostrar como "F-0001-STR"
                                var numeroFactura = "F-0001-STR";
                                var partes = factura.Numero.Split('-');
                                if (partes.Length > 0 && int.TryParse(partes[0], out var num))
                                {
                                    numeroFactura = $"F-{num:D4}-STR";
                                }
                                facturaColumn.Item().Text(numeroFactura).FontSize(14).Bold().FontColor(Colors.Purple.Darken3);
                            });

                            // Estado
                            row.ConstantItem(100).AlignRight().Column(estadoColumn =>
                            {
                                estadoColumn.Item().Text("Estado:").FontSize(9).FontColor(Colors.Grey.Darken2);
                                estadoColumn.Item().PaddingTop(2).Text(factura.Estado).FontSize(11).Bold()
                                    .FontColor(factura.Estado == "Pagada" ? Colors.Green.Darken2 : 
                                              factura.Estado == "Cancelada" ? Colors.Red.Darken2 : 
                                              Colors.Orange.Darken2);
                            });
                        });
                    });

                page.Content()
                    .PaddingVertical(0.8f, Unit.Centimetre)
                    .Column(column =>
                    {
                        column.Spacing(15);

                        // Información del Cliente (simplificada)
                        column.Item().Background(Colors.Grey.Lighten4).Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2)
                            .Column(clienteColumn =>
                            {
                                var cliente = factura.Cliente;
                                clienteColumn.Item().Text("DATOS DEL CLIENTE").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                                clienteColumn.Item().PaddingTop(5).Row(clienteRow =>
                                {
                                    clienteRow.RelativeItem().Column(datosColumn =>
                                    {
                                        datosColumn.Item().Text($"Cliente: {cliente?.Nombre ?? "—"}").FontSize(9);
                                        var cedula = cliente?.Cedula;
                                        if (!string.IsNullOrEmpty(cedula))
                                        {
                                            datosColumn.Item().Text($"Cédula: {cedula}").FontSize(9);
                                        }
                                        datosColumn.Item().Text($"Código: {cliente?.Codigo ?? "—"}").FontSize(9);
                                        datosColumn.Item().Text($"Fecha: {fechaEmision:dd/MM/yyyy}").FontSize(9);
                                    });
                                    clienteRow.RelativeItem().Column(contactoColumn =>
                                    {
                                        var telefono = cliente?.Telefono;
                                        if (!string.IsNullOrEmpty(telefono))
                                        {
                                            contactoColumn.Item().Text($"Teléfono: {telefono}").FontSize(9);
                                        }
                                        var email = cliente?.Email;
                                        if (!string.IsNullOrEmpty(email))
                                        {
                                            contactoColumn.Item().Text($"E-mail: {email}").FontSize(9);
                                        }
                                        contactoColumn.Item().Text($"Mes facturado: {mesFacturado}").FontSize(9);
                                    });
                                });
                            });

                        // Descripción del Servicio Consumido (simplificada)
                        column.Item().Column(servicioColumn =>
                        {
                            servicioColumn.Item().Text("Descripción del servicio consumido").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                            servicioColumn.Item().PaddingTop(5).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.ConstantColumn(120);
                                });

                                // Header de la tabla
                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCellStyle).Text("Descripción del servicio consumido").FontSize(10).Bold();
                                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Monto C$").FontSize(10).Bold();
                                });

                                // Mostrar servicios
                                if (factura.FacturaServicios != null && factura.FacturaServicios.Any())
                                {
                                    foreach (var facturaServicio in factura.FacturaServicios)
                                    {
                                        var servicio = facturaServicio.Servicio;
                                        
                                        // Si tiene múltiples suscripciones, mostrar cada una
                                        if (facturaServicio.Cantidad > 1)
                                        {
                                            var precioUnitario = servicio.Precio;
                                            for (int i = 1; i <= facturaServicio.Cantidad; i++)
                                            {
                                                table.Cell().Element(BodyCellStyle).Column(servicioInfoColumn =>
                                                {
                                                    servicioInfoColumn.Item().Text($"{servicio.Nombre} - Suscripción {i}").FontSize(9);
                                                    if (!string.IsNullOrWhiteSpace(servicio.Descripcion))
                                                    {
                                                        servicioInfoColumn.Item().PaddingTop(2).Text(servicio.Descripcion).FontSize(8).FontColor(Colors.Grey.Darken1);
                                                    }
                                                });
                                                table.Cell().Element(BodyCellStyle).AlignRight().Text($"{precioUnitario:N2}").FontSize(9);
                                            }
                                        }
                                        else
                                        {
                                            table.Cell().Element(BodyCellStyle).Column(servicioInfoColumn =>
                                            {
                                                servicioInfoColumn.Item().Text(servicio.Nombre).FontSize(9);
                                                if (!string.IsNullOrWhiteSpace(servicio.Descripcion))
                                                {
                                                    servicioInfoColumn.Item().PaddingTop(2).Text(servicio.Descripcion).FontSize(8).FontColor(Colors.Grey.Darken1);
                                                }
                                            });
                                            table.Cell().Element(BodyCellStyle).AlignRight().Text($"{servicio.Precio:N2}").FontSize(9);
                                        }
                                    }
                                }
                                else
                                {
                                    // Fallback: mostrar servicio principal
                                    table.Cell().Element(BodyCellStyle).Column(servicioInfoColumn =>
                                    {
                                        servicioInfoColumn.Item().Text(factura.Servicio.Nombre).FontSize(9);
                                        if (!string.IsNullOrWhiteSpace(factura.Servicio.Descripcion))
                                        {
                                            servicioInfoColumn.Item().PaddingTop(2).Text(factura.Servicio.Descripcion).FontSize(8).FontColor(Colors.Grey.Darken1);
                                        }
                                    });
                                    table.Cell().Element(BodyCellStyle).AlignRight().Text($"{factura.Servicio.Precio:N2}").FontSize(9);
                                }

                                // Sub-total (sin proporcional para Streaming)
                                table.Cell().Element(BodyCellStyle).Text("Sub-total C$").FontSize(9);
                                table.Cell().Element(BodyCellStyle).AlignRight().Text($"{factura.Monto:N2}").FontSize(9);

                                // IVA
                                table.Cell().Element(BodyCellStyle).Text("I.V.A. C$").FontSize(9);
                                table.Cell().Element(BodyCellStyle).AlignRight().Text("0.00").FontSize(9);

                                // Método de pago (si hay pagos) - después de IVA
                                var metodoPagoTextoStreaming = ObtenerMetodoPagoTexto(factura);
                                if (!string.IsNullOrEmpty(metodoPagoTextoStreaming))
                                {
                                    table.Cell().Element(BodyCellStyle).Text("Método de pago").FontSize(9);
                                    table.Cell().Element(BodyCellStyle).AlignRight().Text(metodoPagoTextoStreaming).FontSize(9).Bold();
                                }

                                // Total
                                table.Cell().Element(TotalCellStyle).Text("Total C$").FontSize(11).Bold();
                                table.Cell().Element(TotalCellStyle).AlignRight().Text($"{factura.Monto:N2}").FontSize(11).Bold();
                            });
                        });

                        // Sección de Métodos de Pago - Compacta y visual
                        column.Item().PaddingTop(10).PaddingBottom(5).Column(pagoColumn =>
                        {
                            pagoColumn.Item()
                                .Background(Colors.Purple.Lighten5)
                                .Border(2)
                                .BorderColor(Colors.Purple.Darken2)
                                .Padding(10)
                                .Column(metodoPagoColumn =>
                                {
                                    metodoPagoColumn.Item()
                                        .AlignCenter()
                                        .Text("💳 MÉTODOS DE PAGO")
                                        .FontSize(12)
                                        .Bold()
                                        .FontColor(Colors.Purple.Darken3);
                                    
                                    metodoPagoColumn.Item().PaddingTop(5)
                                        .AlignCenter()
                                        .Text("Consulta los métodos de pago disponibles")
                                        .FontSize(9)
                                        .FontColor(Colors.Grey.Darken2);
                                    
                                    metodoPagoColumn.Item().PaddingTop(5)
                                        .AlignCenter()
                                        .Hyperlink("https://www.emsinetsolut.com/#pagos")
                                        .Text("https://www.emsinetsolut.com/#pagos")
                                        .FontSize(10)
                                        .Bold()
                                        .FontColor(Colors.Purple.Darken2)
                                        .Underline();
                                });
                        });
                    });

                page.Footer()
                    .AlignCenter()
                    .PaddingTop(10)
                    .Text(x =>
                    {
                        x.Span("Sistema de Facturación Servicios de Streaming - ").FontSize(7).FontColor(Colors.Grey.Medium);
                        x.Span($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(7).FontColor(Colors.Grey.Medium);
                    });
            });
        });

        return documento.GeneratePdf();
    }

    public byte[] GenerarPdfFactura(Factura factura)
    {
        // Si es Streaming, usar el método específico
        if (factura.Categoria == SD.CategoriaStreaming)
        {
            return GenerarPdfFacturaStreaming(factura);
        }
        
        // Fecha de emisión: usar la fecha de creación de la factura
        var fechaEmision = factura.FechaCreacion;
        // Fecha de vencimiento: día 06 del mes siguiente al mes de facturación
        // Ejemplo: Factura de noviembre → Vence el 6 de diciembre
        var mesSiguiente = factura.MesFacturacion.AddMonths(1);
        var fechaVencimiento = new DateTime(mesSiguiente.Year, mesSiguiente.Month, 6);
        var mesFacturado = factura.MesFacturacion.ToString("MMM/yyyy").ToUpper();
        
        // Calcular multa: 200.00 solo si la factura está pendiente y la fecha actual es después del día 12 a las 11pm
        // La multa se aplica el día 12 del mes siguiente a las 11pm
        var fechaActual = DateTime.Now;
        var fechaAplicacionMulta = new DateTime(mesSiguiente.Year, mesSiguiente.Month, 12, 23, 0, 0); // Día 12 a las 11pm
        var multaPorPagoTardio = 0.00m;
        if (factura.Estado == SD.EstadoFacturaPendiente && fechaActual > fechaAplicacionMulta)
        {
            multaPorPagoTardio = 200.00m;
        }
        
        // Calcular días facturados (para mostrar en el PDF)
        var diasFacturados = CalcularDiasFacturados(factura);
        
        // Calcular monto a pagar: el monto a pagar es el factura.Monto (monto proporcional)
        var montoAPagar = factura.Monto; // El monto de la factura ya es el proporcional calculado
        
        // Cargar logo si existe
        var logoBytes = CargarLogo();
        
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9));

                // Header con Logo e Información de la Empresa
                page.Header()
                    .Column(column =>
                    {
                        column.Item().Row(row =>
                        {
                            // Logo (si existe)
                            if (logoBytes != null)
                            {
                                row.ConstantItem(80).Height(60).AlignLeft().AlignMiddle()
                                    .Image(logoBytes).FitArea();
                            }
                            else
                            {
                                row.ConstantItem(80);
                            }
                            
                            // Información de la Empresa
                            row.RelativeItem().PaddingLeft(10).Column(empresaColumn =>
                            {
                                empresaColumn.Item().Text("Servicio De Internet").FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                                empresaColumn.Item().Text("Sistema de Facturación").FontSize(9).FontColor(Colors.Grey.Darken2);
                                empresaColumn.Item().Text("Nicaragua").FontSize(8).FontColor(Colors.Grey.Medium);
                                empresaColumn.Item().Text("Correo: atencion.al.cliente@emsinetsolut.com").FontSize(8).FontColor(Colors.Grey.Medium);
                                empresaColumn.Item().Text("Teléfonos: 89308058 / 82771485").FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                        });

                        column.Item().PaddingTop(10).Row(row =>
                        {
                            // Número de Factura destacado - Formato F-0001
                            row.RelativeItem().Column(facturaColumn =>
                            {
                                // Extraer el número del formato "0001-Nombre-MMYYYY" y mostrar como "F-0001"
                                var numeroFactura = "F-0001";
                                var partes = factura.Numero.Split('-');
                                if (partes.Length > 0 && int.TryParse(partes[0], out var num))
                                {
                                    numeroFactura = $"F-{num:D4}";
                                }
                                facturaColumn.Item().Text(numeroFactura).FontSize(14).Bold().FontColor(Colors.Blue.Darken3);
                            });

                            // Estado
                            row.ConstantItem(100).AlignRight().Column(estadoColumn =>
                            {
                                estadoColumn.Item().Text("Estado:").FontSize(9).FontColor(Colors.Grey.Darken2);
                                estadoColumn.Item().PaddingTop(2).Text(factura.Estado).FontSize(11).Bold()
                                    .FontColor(factura.Estado == "Pagada" ? Colors.Green.Darken2 : 
                                              factura.Estado == "Cancelada" ? Colors.Red.Darken2 : 
                                              Colors.Orange.Darken2);
                            });
                        });
                    });

                page.Content()
                    .PaddingVertical(0.8f, Unit.Centimetre)
                    .Column(column =>
                    {
                        column.Spacing(15);

                        // Información del Cliente (formato similar a la imagen)
                        column.Item().Background(Colors.Grey.Lighten4).Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2)
                            .Column(clienteColumn =>
                            {
                                var cliente = factura.Cliente;
                                clienteColumn.Item().Text("DATOS DEL CLIENTE").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                                clienteColumn.Item().PaddingTop(5).Row(clienteRow =>
                                {
                                    clienteRow.RelativeItem().Column(datosColumn =>
                                    {
                                        datosColumn.Item().Text($"Cliente: {cliente?.Nombre ?? "—"}").FontSize(9);
                                        var cedula = cliente?.Cedula;
                                        if (!string.IsNullOrEmpty(cedula))
                                        {
                                            datosColumn.Item().Text($"Cédula: {cedula}").FontSize(9);
                                        }
                                        datosColumn.Item().Text($"Código: {cliente?.Codigo ?? "—"}").FontSize(9);
                                        datosColumn.Item().Text($"Fecha: {fechaEmision:dd/MM/yyyy}").FontSize(9);
                                    });
                                    clienteRow.RelativeItem().Column(contactoColumn =>
                                    {
                                        var telefono = cliente?.Telefono;
                                        if (!string.IsNullOrEmpty(telefono))
                                        {
                                            contactoColumn.Item().Text($"Teléfono: {telefono}").FontSize(9);
                                        }
                                        var email = cliente?.Email;
                                        if (!string.IsNullOrEmpty(email))
                                        {
                                            contactoColumn.Item().Text($"E-mail: {email}").FontSize(9);
                                        }
                                        contactoColumn.Item().Text($"Mes facturado: {mesFacturado}").FontSize(9);
                                        contactoColumn.Item().Text($"Días Facturado: {diasFacturados}").FontSize(9);
                                        contactoColumn.Item().Text($"Vencimiento: {fechaVencimiento:dd/MM/yyyy}").FontSize(10).Bold().FontColor(Colors.Red.Darken2);
                                    });
                                });
                            });

                        // Descripción del Servicio Consumido
                        column.Item().Column(servicioColumn =>
                        {
                            servicioColumn.Item().Text("Descripción del servicio consumido").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                            servicioColumn.Item().PaddingTop(5).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.ConstantColumn(120);
                                });

                                // Header de la tabla
                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCellStyle).Text("Descripción del servicio consumido").FontSize(10).Bold();
                                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Monto C$").FontSize(10).Bold();
                                });

                                // Calcular subtotal sin proporcional para mostrar descuento si aplica
                                decimal subtotalSinProporcional = 0;
                                bool subtotalCalculadoEnLoop = false;
                                
                                // Si la factura tiene FacturaServicios (múltiples servicios), mostrar todos
                                if (factura.FacturaServicios != null && factura.FacturaServicios.Any())
                                {
                                    foreach (var facturaServicio in factura.FacturaServicios)
                                    {
                                        var servicio = facturaServicio.Servicio;
                                        
                                        // Verificar que el servicio no sea null
                                        if (servicio == null)
                                            continue;
                                        
                                        // Si es Streaming y tiene múltiples suscripciones, mostrar cada una
                                        if (servicio.Categoria == SD.CategoriaStreaming && facturaServicio.Cantidad > 1)
                                        {
                                            // Mostrar el precio completo del servicio por cada suscripción
                                            var precioUnitario = servicio.Precio;
                                            subtotalSinProporcional += precioUnitario * facturaServicio.Cantidad;
                                            subtotalCalculadoEnLoop = true;
                                            
                                            for (int i = 1; i <= facturaServicio.Cantidad; i++)
                                            {
                                                table.Cell().Element(BodyCellStyle).Column(servicioInfoColumn =>
                                                {
                                                    servicioInfoColumn.Item().Text($"{servicio.Nombre} - Suscripción {i}").FontSize(9);
                                                    if (!string.IsNullOrWhiteSpace(servicio.Descripcion))
                                                    {
                                                        servicioInfoColumn.Item().PaddingTop(2).Text(servicio.Descripcion).FontSize(8).FontColor(Colors.Grey.Darken1);
                                                    }
                                                });
                                                table.Cell().Element(BodyCellStyle).AlignRight().Text(FormatearMontoEntero(precioUnitario)).FontSize(9);
                                            }
                                        }
                                        else
                                        {
                                            // Servicio único o Internet - mostrar precio completo
                                            var precioCompleto = servicio.Precio;
                                            subtotalSinProporcional += precioCompleto;
                                            subtotalCalculadoEnLoop = true;
                                            
                                            table.Cell().Element(BodyCellStyle).Column(servicioInfoColumn =>
                                            {
                                                servicioInfoColumn.Item().Text(servicio.Nombre).FontSize(9);
                                                if (!string.IsNullOrWhiteSpace(servicio.Descripcion))
                                                {
                                                    servicioInfoColumn.Item().PaddingTop(2).Text(servicio.Descripcion).FontSize(8).FontColor(Colors.Grey.Darken1);
                                                }
                                            });
                                            table.Cell().Element(BodyCellStyle).AlignRight().Text(FormatearMontoEntero(precioCompleto)).FontSize(9);
                                        }
                                    }
                                }
                                else
                                {
                                    // Fallback: mostrar servicio principal (compatibilidad con facturas antiguas)
                                    if (factura.Servicio != null)
                                    {
                                        subtotalSinProporcional = factura.Servicio.Precio;
                                        subtotalCalculadoEnLoop = true;
                                        table.Cell().Element(BodyCellStyle).Column(servicioInfoColumn =>
                                        {
                                            servicioInfoColumn.Item().Text(factura.Servicio.Nombre).FontSize(9);
                                            if (!string.IsNullOrWhiteSpace(factura.Servicio.Descripcion))
                                            {
                                                servicioInfoColumn.Item().PaddingTop(2).Text(factura.Servicio.Descripcion).FontSize(8).FontColor(Colors.Grey.Darken1);
                                            }
                                        });
                                        table.Cell().Element(BodyCellStyle).AlignRight().Text(FormatearMontoEntero(factura.Servicio.Precio)).FontSize(9);
                                    }
                                }
                                
                                // Si no se calculó el subtotal en el loop anterior (caso edge), calcularlo
                                if (!subtotalCalculadoEnLoop)
                                {
                                    subtotalSinProporcional = CalcularSubtotalSinProporcional(factura);
                                }

                                // Sub-total (precio completo sin proporcional)
                                table.Cell().Element(BodyCellStyle).Text("Sub-total C$").FontSize(9);
                                table.Cell().Element(BodyCellStyle).AlignRight().Text(FormatearMontoEntero(subtotalSinProporcional)).FontSize(9);

                                // Descuento proporcional (solo si hay diferencia) - mostrar el descuento, no el monto a pagar
                                if (subtotalSinProporcional > 0 && Math.Abs(subtotalSinProporcional - factura.Monto) > 0.01m)
                                {
                                    var descuentoProporcional = subtotalSinProporcional - factura.Monto; // El descuento es la diferencia
                                    table.Cell().Element(BodyCellStyle).Text("Descuento proporcional C$").FontSize(9).FontColor(Colors.Red.Darken1);
                                    table.Cell().Element(BodyCellStyle).AlignRight().Text($"-{FormatearMontoEntero(descuentoProporcional)}").FontSize(9).FontColor(Colors.Red.Darken1);
                                }

                                // IVA
                                table.Cell().Element(BodyCellStyle).Text("I.V.A. C$").FontSize(9);
                                table.Cell().Element(BodyCellStyle).AlignRight().Text("0").FontSize(9);

                                // Método de pago (si hay pagos) - después de IVA
                                var metodoPagoTextoInternet = ObtenerMetodoPagoTexto(factura);
                                if (!string.IsNullOrEmpty(metodoPagoTextoInternet))
                                {
                                    table.Cell().Element(BodyCellStyle).Text("Método de pago").FontSize(9);
                                    table.Cell().Element(BodyCellStyle).AlignRight().Text(metodoPagoTextoInternet).FontSize(9).Bold();
                                }

                                // Total: El total es el monto de la factura (monto proporcional)
                                table.Cell().Element(TotalCellStyle).Text("Total C$").FontSize(11).Bold();
                                table.Cell().Element(TotalCellStyle).AlignRight().Text(FormatearMontoEntero(factura.Monto)).FontSize(11).Bold();
                            });
                        });

                        // Estado de la Cuenta cuando hay pagos
                        if (factura.Pagos != null && factura.Pagos.Any())
                        {
                            // Calcular el monto total pagado sumando los montos aplicados de cada pago
                            var montoPagado = 0m;
                            if (_context != null)
                            {
                                foreach (var pago in factura.Pagos)
                                {
                                    if (pago.FacturaId == factura.Id)
                                    {
                                        montoPagado += pago.Monto;
                                    }
                                    else
                                    {
                                        var pagoFactura = _context.PagoFacturas
                                            .FirstOrDefault(pf => pf.PagoId == pago.Id && pf.FacturaId == factura.Id);
                                        if (pagoFactura != null)
                                        {
                                            montoPagado += pagoFactura.MontoAplicado;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                montoPagado = factura.Pagos.Sum(p => p.Monto);
                            }
                            column.Item().Column(estadoColumn =>
                            {
                                estadoColumn.Item().Text("Estado de la cuenta").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                                estadoColumn.Item().PaddingTop(5).Table(estadoTable =>
                                {
                                    estadoTable.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(3);
                                        columns.ConstantColumn(120);
                                    });

                                    estadoTable.Header(header =>
                                    {
                                        header.Cell().Element(HeaderCellStyle).Text("Estado de la cuenta").FontSize(10).Bold();
                                        header.Cell().Element(HeaderCellStyle).AlignRight().Text("C$").FontSize(10).Bold();
                                    });

                                    estadoTable.Cell().Element(BodyCellStyle).Text("Saldo inicial del periodo").FontSize(9);
                                    estadoTable.Cell().Element(BodyCellStyle).AlignRight().Text("0").FontSize(9);

                                    estadoTable.Cell().Element(BodyCellStyle).Text("Monto consumido").FontSize(9);
                                    estadoTable.Cell().Element(BodyCellStyle).AlignRight().Text(FormatearMontoEntero(montoAPagar)).FontSize(9);

                                    estadoTable.Cell().Element(BodyCellStyle).Text("Multa por pago tardío").FontSize(9);
                                    estadoTable.Cell().Element(BodyCellStyle).AlignRight().Text(FormatearMontoEntero(multaPorPagoTardio)).FontSize(9);

                                    estadoTable.Cell().Element(BodyCellStyle).Text("Monto pagado").FontSize(9);
                                    estadoTable.Cell().Element(BodyCellStyle).AlignRight().Text(FormatearMontoEntero(montoPagado)).FontSize(9);

                                    estadoTable.Cell().Element(BodyCellStyle).Text("Saldo final del periodo").FontSize(9);
                                    var saldoFinal = montoAPagar - montoPagado + multaPorPagoTardio;
                                    estadoTable.Cell().Element(BodyCellStyle).AlignRight().Text(FormatearMontoEntero(saldoFinal)).FontSize(9);

                                    // Monto total a pagar: usar el monto a pagar (descuento proporcional si aplica)
                                    var montoTotalPagar = montoAPagar;
                                    estadoTable.Cell().Element(TotalCellStyle).Text("Monto total a pagar").FontSize(11).Bold();
                                    estadoTable.Cell().Element(TotalCellStyle).AlignRight().Text(FormatearMontoEntero(montoTotalPagar)).FontSize(11).Bold();
                                });
                            });
                        }
                        else
                        {
                            // Estado de la cuenta sin pagos
                            column.Item().Column(estadoColumn =>
                            {
                                estadoColumn.Item().Text("Estado de la cuenta").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                                estadoColumn.Item().PaddingTop(5).Table(estadoTable =>
                                {
                                    estadoTable.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(3);
                                        columns.ConstantColumn(120);
                                    });

                                    estadoTable.Header(header =>
                                    {
                                        header.Cell().Element(HeaderCellStyle).Text("Estado de la cuenta").FontSize(10).Bold();
                                        header.Cell().Element(HeaderCellStyle).AlignRight().Text("C$").FontSize(10).Bold();
                                    });

                                    estadoTable.Cell().Element(BodyCellStyle).Text("Saldo inicial del periodo").FontSize(9);
                                    estadoTable.Cell().Element(BodyCellStyle).AlignRight().Text("0").FontSize(9);

                                    estadoTable.Cell().Element(BodyCellStyle).Text("Monto consumido").FontSize(9);
                                    estadoTable.Cell().Element(BodyCellStyle).AlignRight().Text(FormatearMontoEntero(montoAPagar)).FontSize(9);

                                    estadoTable.Cell().Element(BodyCellStyle).Text("Multa por pago tardío").FontSize(9);
                                    estadoTable.Cell().Element(BodyCellStyle).AlignRight().Text(FormatearMontoEntero(multaPorPagoTardio)).FontSize(9);

                                    estadoTable.Cell().Element(BodyCellStyle).Text("Monto pagado").FontSize(9);
                                    estadoTable.Cell().Element(BodyCellStyle).AlignRight().Text("0").FontSize(9);

                                    estadoTable.Cell().Element(BodyCellStyle).Text("Saldo final del periodo").FontSize(9);
                                    estadoTable.Cell().Element(BodyCellStyle).AlignRight().Text("0").FontSize(9);

                                    var montoTotalPagar = montoAPagar + multaPorPagoTardio;
                                    estadoTable.Cell().Element(TotalCellStyle).Text("Monto total a pagar").FontSize(11).Bold();
                                    estadoTable.Cell().Element(TotalCellStyle).AlignRight().Text(FormatearMontoEntero(montoTotalPagar)).FontSize(11).Bold();
                                });
                            });
                        }

                        // Sección de Métodos de Pago - Compacta y visual
                        column.Item().PaddingTop(10).PaddingBottom(5).Column(pagoColumn =>
                        {
                            pagoColumn.Item()
                                .Background(Colors.Blue.Lighten5)
                                .Border(2)
                                .BorderColor(Colors.Blue.Darken2)
                                .Padding(10)
                                .Column(metodoPagoColumn =>
                                {
                                    metodoPagoColumn.Item()
                                        .AlignCenter()
                                        .Text("💳 MÉTODOS DE PAGO")
                                        .FontSize(12)
                                        .Bold()
                                        .FontColor(Colors.Blue.Darken3);
                                    
                                    metodoPagoColumn.Item().PaddingTop(5)
                                        .AlignCenter()
                                        .Text("Consulta los métodos de pago disponibles")
                                        .FontSize(9)
                                        .FontColor(Colors.Grey.Darken2);
                                    
                                    metodoPagoColumn.Item().PaddingTop(5)
                                        .AlignCenter()
                                        .Hyperlink("https://www.emsinetsolut.com/#pagos")
                                        .Text("https://www.emsinetsolut.com/#pagos")
                                        .FontSize(10)
                                        .Bold()
                                        .FontColor(Colors.Blue.Darken2)
                                        .Underline();
                                });
                        });
                    });

                page.Footer()
                    .AlignCenter()
                    .PaddingTop(10)
                    .Text(x =>
                    {
                        x.Span("Sistema de Facturación Servicio De Internet - ").FontSize(7).FontColor(Colors.Grey.Medium);
                        x.Span($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(7).FontColor(Colors.Grey.Medium);
                    });
            });
        });

        return documento.GeneratePdf();
    }

    public byte[] GenerarPdfPedido(Factura pedido)
    {
        var fecha = pedido.FechaCreacion;
        var cliente = pedido.Cliente?.Nombre
            ?? pedido.DeliveryClienteNombre
            ?? "Cliente";
        var mesa = pedido.Mesa?.Numero ?? "S/M";
        var tipo = pedido.OrigenPedido;

        var lineas = (pedido.FacturaServicios ?? new List<FacturaServicio>())
            .Select(i => new
            {
                Producto = i.Servicio?.Nombre ?? $"Producto #{i.ServicioId}",
                i.Cantidad,
                i.PrecioUnitario,
                Subtotal = i.Monto
            })
            .ToList();

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("BarRestPOS - Ticket de Pedido").FontSize(16).Bold();
                    col.Item().Text($"Pedido: {pedido.Numero}").FontSize(11);
                    col.Item().Text($"Fecha: {fecha:dd/MM/yyyy HH:mm}  |  Tipo: {tipo}  |  Mesa: {mesa}").FontSize(9);
                    col.Item().Text($"Cliente: {cliente}").FontSize(9);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4);
                        c.ConstantColumn(60);
                        c.ConstantColumn(80);
                        c.ConstantColumn(90);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCellStyle).Text("Producto").Bold();
                        h.Cell().Element(HeaderCellStyle).AlignRight().Text("Cant").Bold();
                        h.Cell().Element(HeaderCellStyle).AlignRight().Text("P. Unit").Bold();
                        h.Cell().Element(HeaderCellStyle).AlignRight().Text("Subtotal").Bold();
                    });

                    foreach (var l in lineas)
                    {
                        table.Cell().Element(BodyCellStyle).Text(l.Producto);
                        table.Cell().Element(BodyCellStyle).AlignRight().Text(l.Cantidad.ToString());
                        table.Cell().Element(BodyCellStyle).AlignRight().Text(l.PrecioUnitario.ToString("N2"));
                        table.Cell().Element(BodyCellStyle).AlignRight().Text(l.Subtotal.ToString("N2"));
                    }

                    table.Cell().Element(TotalCellStyle).Text("TOTAL").Bold();
                    table.Cell().Element(TotalCellStyle).Text("");
                    table.Cell().Element(TotalCellStyle).Text("");
                    table.Cell().Element(TotalCellStyle).AlignRight().Text(pedido.Monto.ToString("N2")).Bold();
                });

                page.Footer().AlignCenter().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");
            });
        });

        return documento.GeneratePdf();
    }

    /// <summary>
    /// Formatea un monto como entero para facturas de Internet (redondea)
    /// </summary>
    private string FormatearMontoEntero(decimal monto)
    {
        // Redondear: si pasa de 5 (>= 0.5), redondea hacia arriba
        var montoRedondeado = Math.Round(monto, MidpointRounding.AwayFromZero);
        return ((int)montoRedondeado).ToString("N0");
    }

    /// <summary>
    /// Formatea un monto según la categoría: enteros para Internet, decimales para Streaming
    /// </summary>
    private string FormatearMontoSegunCategoria(decimal monto, string categoria)
    {
        if (categoria == SD.CategoriaInternet)
        {
            return FormatearMontoEntero(monto);
        }
        return monto.ToString("N2");
    }

    /// <summary>
    /// Calcula el subtotal sin proporcional de una factura
    /// </summary>
    private decimal CalcularSubtotalSinProporcional(Factura factura)
    {
        decimal subtotal = 0;
        
        if (factura.FacturaServicios != null && factura.FacturaServicios.Any())
        {
            foreach (var facturaServicio in factura.FacturaServicios)
            {
                var servicio = facturaServicio.Servicio;
                if (servicio != null)
                {
                    if (servicio.Categoria == SD.CategoriaStreaming && facturaServicio.Cantidad > 1)
                    {
                        subtotal += servicio.Precio * facturaServicio.Cantidad;
                    }
                    else
                    {
                        subtotal += servicio.Precio;
                    }
                }
            }
        }
        else if (factura.Servicio != null)
        {
            subtotal = factura.Servicio.Precio;
        }
        
        return subtotal;
    }

    /// <summary>
    /// Carga el logo desde el sistema de archivos
    /// </summary>
    private byte[]? CargarLogo()
    {
        if (_environment == null)
        {
            return null;
        }
        
        var logoPath = Path.Combine(_environment.WebRootPath, "images", "logo.png");
        if (File.Exists(logoPath))
        {
            return File.ReadAllBytes(logoPath);
        }
        
        return null;
    }

    /// <summary>
    /// Obtiene el texto del método de pago de una factura
    /// </summary>
    private string? ObtenerMetodoPagoTexto(Factura factura)
    {
        if (factura.Pagos == null || !factura.Pagos.Any())
        {
            return null;
        }
        
        var primerPago = factura.Pagos.OrderBy(p => p.FechaPago).First();
        
        if (primerPago.TipoPago == SD.TipoPagoFisico)
            return "Físico";
        else if (primerPago.TipoPago == SD.TipoPagoElectronico)
            return "Electrónico";
        else if (primerPago.TipoPago == SD.TipoPagoMixto)
            return "Mixto";
        
        return null;
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container
            .Background(Colors.Grey.Lighten3)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten1)
            .PaddingVertical(8)
            .PaddingHorizontal(10);
    }

    private static IContainer BodyCellStyle(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(6)
            .PaddingHorizontal(10);
    }

    private static IContainer TotalCellStyle(IContainer container)
    {
        return container
            .Background(Colors.Grey.Lighten4)
            .BorderTop(2)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Medium)
            .PaddingVertical(8)
            .PaddingHorizontal(10);
    }

    /// <summary>
    /// Calcula los días facturados basándose en si hay descuento proporcional
    /// </summary>
    private int CalcularDiasFacturados(Factura factura)
    {
        // Si no hay contexto de base de datos, retornar días del mes completo
        if (_context == null)
        {
            return DateTime.DaysInMonth(factura.MesFacturacion.Year, factura.MesFacturacion.Month);
        }

        // Calcular subtotal sin proporcional para verificar si hay descuento
        var subtotalSinProporcional = CalcularSubtotalSinProporcional(factura);

        // Si no hay descuento proporcional (o la diferencia es muy pequeña), es mes completo
        if (Math.Abs(subtotalSinProporcional - factura.Monto) <= 0.01m)
        {
            // Mes completo: retornar días del mes de facturación
            return DateTime.DaysInMonth(factura.MesFacturacion.Year, factura.MesFacturacion.Month);
        }

        // Hay descuento proporcional: calcular días desde fecha de inicio hasta día 5 del mes siguiente
        // Obtener el primer servicio de Internet de la factura para calcular días
        var primerServicioInternet = factura.FacturaServicios?
            .FirstOrDefault(fs => fs.Servicio?.Categoria == SD.CategoriaInternet)?.Servicio
            ?? factura.Servicio;

        if (primerServicioInternet == null || primerServicioInternet.Categoria != SD.CategoriaInternet)
        {
            // Si no hay servicio de Internet, retornar días del mes completo
            return DateTime.DaysInMonth(factura.MesFacturacion.Year, factura.MesFacturacion.Month);
        }

        if (factura.Cliente == null)
        {
            return DateTime.DaysInMonth(factura.MesFacturacion.Year, factura.MesFacturacion.Month);
        }

        // IMPORTANTE: Usar la fecha de creación del cliente para el cálculo proporcional
        var fechaInicio = factura.Cliente.FechaCreacion;
        var fechaInicioDate = fechaInicio.Date;
        var primerDiaMesFacturacion = new DateTime(factura.MesFacturacion.Year, factura.MesFacturacion.Month, 1);
        var ultimoDiaMesFacturacion = primerDiaMesFacturacion.AddMonths(1).AddDays(-1);

        // Si inició antes del mes de facturación o después, es mes completo
        if (fechaInicioDate < primerDiaMesFacturacion || fechaInicioDate > ultimoDiaMesFacturacion)
        {
            return DateTime.DaysInMonth(factura.MesFacturacion.Year, factura.MesFacturacion.Month);
        }

        // Si inició el día 5 o antes, es mes completo
        if (fechaInicioDate.Day <= 5)
        {
            return DateTime.DaysInMonth(factura.MesFacturacion.Year, factura.MesFacturacion.Month);
        }

        // El ciclo de facturación es del día 5 al día 5 (del 5 de un mes al 5 del siguiente mes = 30 días)
        // Los días facturados se cuentan desde la fecha de inicio hasta el día 5 del mes siguiente
        // Ejemplo: Cliente entra el 13 nov → cuenta del 13 nov al 5 dic = 23 días
        var mesSiguiente = factura.MesFacturacion.AddMonths(1);
        var dia5MesSiguiente = new DateTime(mesSiguiente.Year, mesSiguiente.Month, 5);
        
        // Calcular días facturados: desde fecha de inicio hasta el día 5 del mes siguiente (incluyendo ambos días)
        var diasFacturados = (dia5MesSiguiente.Date - fechaInicioDate.Date).Days + 1;

        // Asegurar que los días sean válidos
        if (diasFacturados <= 0)
        {
            return DateTime.DaysInMonth(factura.MesFacturacion.Year, factura.MesFacturacion.Month);
        }

        return diasFacturados;
    }
}

