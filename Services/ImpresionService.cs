using System;
using System.Collections.Generic;
using System.Linq;
using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace BarRestPOS.Services;

/// <summary>
/// Servicio para generar tickets de impresión térmica nativa (ESC/POS)
/// </summary>
public class ImpresionService : IImpresionService
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public ImpresionService(IConfiguration configuration, ApplicationDbContext context, IWebHostEnvironment env)
    {
        _configuration = configuration;
        _context = context;
        _env = env;
    }

    private string ObtenerNombreRestaurante()
    {
        var nombre = _context.Configuraciones
            .AsNoTracking()
            .Where(c => c.Clave == "Tickets:NombreRestaurante")
            .Select(c => c.Valor)
            .FirstOrDefault()?.Trim();
            
        if (string.IsNullOrEmpty(nombre))
        {
            nombre = _configuration["Tickets:NombreRestaurante"]?.Trim() ?? "Bar Rest POS";
        }
        return nombre;
    }

    private string ObtenerDireccionRestaurante()
    {
        var direccion = _context.Configuraciones
            .AsNoTracking()
            .Where(c => c.Clave == "Tickets:DireccionRestaurante")
            .Select(c => c.Valor)
            .FirstOrDefault()?.Trim();
            
        if (string.IsNullOrEmpty(direccion))
        {
            direccion = _configuration["Tickets:DireccionRestaurante"]?.Trim() ?? "";
        }
        return direccion;
    }

    private string ObtenerTelefonoRestaurante()
    {
        var telefono = _context.Configuraciones
            .AsNoTracking()
            .Where(c => c.Clave == "Tickets:TelefonoRestaurante")
            .Select(c => c.Valor)
            .FirstOrDefault()?.Trim();
            
        if (string.IsNullOrEmpty(telefono))
        {
            telefono = _configuration["Tickets:TelefonoRestaurante"]?.Trim() ?? "";
        }
        return telefono;
    }

    private string ObtenerRucRestaurante()
    {
        var ruc = _context.Configuraciones
            .AsNoTracking()
            .Where(c => c.Clave == "Tickets:RucRestaurante")
            .Select(c => c.Valor)
            .FirstOrDefault()?.Trim();
            
        if (string.IsNullOrEmpty(ruc))
        {
            ruc = _configuration["Tickets:RucRestaurante"]?.Trim() ?? "";
        }
        return ruc;
    }

    private string ObtenerLogoFisico()
    {
        var logoUrl = _context.Configuraciones
            .Where(c => c.Clave == "Tickets:LogoUrl")
            .Select(c => c.Valor)
            .FirstOrDefault()?.Trim();
            
        if (string.IsNullOrEmpty(logoUrl)) return null;

        if (logoUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var persistentUploadsDir = Path.Combine(appDataPath, "BarRestPOS", "uploads");
            
            var relativePath = logoUrl.Substring("/uploads/".Length).Replace("/", "\\");
            var fullPath = Path.Combine(persistentUploadsDir, relativePath);
            
            if (File.Exists(fullPath)) return fullPath;
        }

        // Limpiar URL fallback
        var cleanPath = logoUrl.Replace("/api/v1/impresion", "").TrimStart('/');
        var fallbackPath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, cleanPath);
        
        return File.Exists(fallbackPath) ? fallbackPath : null;
    }

    private EscPosBuilder ConstruirCabecera(string tipoTicket, string numero, Factura orden, DateTime fecha)
    {
        var esc = new EscPosBuilder();
        var nombreRest = ObtenerNombreRestaurante();
        var logoPath = ObtenerLogoFisico();

        if (!string.IsNullOrEmpty(logoPath))
        {
            esc.PrintImage(logoPath);
        }

        esc.AlignCenter()
           .DoubleSizeFont()
           .BoldOn()
           .PrintLine(nombreRest)
           .NormalFont()
           .BoldOff();

        var direccion = ObtenerDireccionRestaurante();
        if (!string.IsNullOrEmpty(direccion))
        {
            esc.PrintLine(direccion);
        }

        var telefono = ObtenerTelefonoRestaurante();
        if (!string.IsNullOrEmpty(telefono))
        {
            esc.PrintLine($"TEL: {telefono}");
        }

        var ruc = ObtenerRucRestaurante();
        if (!string.IsNullOrEmpty(ruc))
        {
            esc.PrintLine($"RUC: {ruc}");
        }

        var mesaNum = orden.Mesa?.Numero ?? "S/M";
        var mesaStr = mesaNum == "S/M" || mesaNum.StartsWith("Mesa", StringComparison.OrdinalIgnoreCase) ? mesaNum : $"Mesa {mesaNum}";

        return esc.DrawDivider()
           .AlignLeft()
           .BoldOn()
           .PrintLine($"{tipoTicket}: {numero}")
           .BoldOff()
           .PrintLine($"FECHA:  {fecha:dd/MM/yyyy HH:mm}")
           .PrintLine($"ORIGEN: {mesaStr}")
           .PrintLine($"MESERO: {orden.Mesero?.NombreCompleto ?? "Sin registro"}")
           .DrawDivider();
    }

    private void ConstruirPiePagina(EscPosBuilder esc, string mensajeDespedida)
    {
        esc.AlignCenter()
           .PrintLine(mensajeDespedida)
           .PrintLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"))
           .FeedLines(4)
           .CutPaper();
    }

    private static string StringFragmentOpcionesLinea(FacturaServicio item)
    {
        var opts = item.OpcionesSeleccionadas;
        if (opts == null || opts.Count == 0) return "";
        return string.Join(" | ",
            opts
                .OrderBy(o => o.NombreGrupo)
                .ThenBy(o => o.NombreOpcion)
                .Select(o => o.NombreOpcion)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList());
    }

    private EscPosBuilder ConstruirCabeceraCocinaBar(string tipoTicket, string numero, Factura orden, DateTime fecha)
    {
        var esc = new EscPosBuilder();
        esc.AlignCenter()
           .BoldOn()
           .PrintLine("================================================")
           .PrintLine($"** {tipoTicket.ToUpper()} **")
           .PrintLine("================================================")
           .NormalFont()
           .BoldOff()
           .AlignLeft();

        string mesaStr;
        if (string.Equals(orden.OrigenPedido, SD.OrigenPedidoDelivery, StringComparison.OrdinalIgnoreCase))
        {
            mesaStr = "ORIGEN: Delivery";
        }
        else if (!string.IsNullOrEmpty(orden.OrigenPedido) && orden.OrigenPedido.Trim().ToLower() != "salon")
        {
            mesaStr = $"ORIGEN: {orden.OrigenPedido}";
        }
        else
        {
            var mesaNum = orden.Mesa?.Numero ?? "S/M";
            var mStr = mesaNum == "S/M" || mesaNum.StartsWith("Mesa", StringComparison.OrdinalIgnoreCase) ? mesaNum : $"Mesa {mesaNum}";
            // Pedidos de salón: ORIGEN con número de mesa, sin nombre de ubicación
            mesaStr = $"ORIGEN: {mStr}";
        }
        var ordenStr = $"ORDEN: #{numero}";
        var meseroStr = $"MESERO: {orden.Mesero?.NombreCompleto ?? "Sin registro"}";

        esc.PrintLine(ordenStr);
        esc.PrintLine(mesaStr);
        esc.PrintLine(meseroStr);

        if (string.Equals(orden.OrigenPedido, SD.OrigenPedidoDelivery, StringComparison.OrdinalIgnoreCase))
        {
            // Solo imprimir si tiene dato, igual que TEL y DIR
            if (!string.IsNullOrWhiteSpace(orden.DeliveryClienteNombre))
            {
                esc.BoldOn()
                   .PrintLine($"CLIENTE: {orden.DeliveryClienteNombre}")
                   .BoldOff();
            }
            if (!string.IsNullOrWhiteSpace(orden.DeliveryClienteTelefono))
            {
                esc.PrintLine($"TEL:     {orden.DeliveryClienteTelefono}");
            }
            if (!string.IsNullOrWhiteSpace(orden.DeliveryClienteDireccion))
            {
                esc.PrintLine($"DIR:     {orden.DeliveryClienteDireccion}");
            }
        }

        esc.PrintLine("================================================");

        return esc;
    }

    private void PrintCardLine(EscPosBuilder esc, string content, string indent = "")
    {
        if (content == null) return;

        int maxLen = 42;
        int indentLen = indent.Length;
        int contentLen = maxLen - indentLen;
        if (contentLen <= 0) contentLen = maxLen;

        var list = new List<string>();
        string remaining = content;

        while (remaining.Length > 0)
        {
            if (remaining.Length <= contentLen)
            {
                list.Add(remaining);
                break;
            }
            else
            {
                int splitIdx = remaining.LastIndexOf(' ', contentLen);
                if (splitIdx <= 0)
                {
                    splitIdx = contentLen;
                }

                list.Add(remaining.Substring(0, splitIdx).TrimEnd());
                remaining = remaining.Substring(splitIdx).TrimStart();
            }
        }

        bool isFirst = true;
        foreach (var line in list)
        {
            var lineWithIndent = isFirst ? (indent + line) : (new string(' ', indentLen) + line);
            isFirst = false;

            var padded = lineWithIndent.PadRight(42);
            if (padded.Length > 42) padded = padded.Substring(0, 42);
            esc.PrintLine($"|  {padded}  |");
        }
    }

    private EscPosBuilder BuildTicketCocina(Factura orden, List<int>? lineasFilter = null)
    {
        var items = CocinaCatalogoHelper.LineasCocina(orden.FacturaServicios);

        string titulo = "COMANDA DE COCINA";
        List<FacturaServicio> itemsParaImprimir;

        if (lineasFilter != null && lineasFilter.Count > 0)
        {
            itemsParaImprimir = items.Where(i => lineasFilter.Contains(i.Id)).ToList();
            // Si el filtro abarca TODOS los artículos de cocina → primer envío completo
            bool esPrimerEnvio = items.All(i => lineasFilter.Contains(i.Id));
            titulo = esPrimerEnvio ? "COMANDA DE COCINA" : "PEDIDO EXTRA - COCINA";
        }
        else
        {
            bool tieneEnPreparacion = items.Any(i => i.Estado == "En Preparación" || i.Estado == "Pendiente");
            bool tieneListosOEntregados = items.Any(i => i.Estado == "Listo" || i.Estado == "Entregado");

            if (tieneEnPreparacion && tieneListosOEntregados)
            {
                titulo = "PEDIDO EXTRA - COCINA";
                itemsParaImprimir = items.Where(i => i.Estado == "En Preparación" || i.Estado == "Pendiente").ToList();
            }
            else if (!tieneEnPreparacion)
            {
                titulo = "REIMPRESION COCINA";
                itemsParaImprimir = items.ToList();
            }
            else
            {
                itemsParaImprimir = items.ToList();
            }
        }

        var esc = ConstruirCabeceraCocinaBar(titulo, orden.Numero, orden, orden.FechaCreacion);

        bool first = true;
        foreach (var item in itemsParaImprimir)
        {
            if (first)
            {
                esc.PrintLine("+----------------------------------------------+");
                first = false;
            }

            var prodNombre = (item.Servicio?.Nombre ?? "Producto").ToUpper();
            PrintCardLine(esc, $"[ {item.Cantidad} ]  {prodNombre}", "");

            var opciones = StringFragmentOpcionesLinea(item);
            if (!string.IsNullOrEmpty(opciones))
            {
                PrintCardLine(esc, $"--> {opciones}", "       ");
            }

            if (!string.IsNullOrEmpty(item.Notas))
            {
                PrintCardLine(esc, $"(¡) NOTA: {item.Notas}", "       ");
            }

            esc.PrintLine("+----------------------------------------------+");
        }

        if (!string.IsNullOrEmpty(orden.Observaciones))
        {
            if (first)
            {
                esc.PrintLine("+----------------------------------------------+");
            }
            PrintCardLine(esc, $"OBS: {orden.Observaciones}", "");
            esc.PrintLine("+----------------------------------------------+");
        }

        ConstruirPiePagina(esc, "¡Gracias por su trabajo!");
        return esc;
    }

    private EscPosBuilder BuildTicketBar(Factura orden, List<int>? lineasFilter = null)
    {
        var items = CocinaCatalogoHelper.LineasBar(orden.FacturaServicios);

        string titulo = "COMANDA DE BAR";
        List<FacturaServicio> itemsParaImprimir;

        if (lineasFilter != null && lineasFilter.Count > 0)
        {
            itemsParaImprimir = items.Where(i => lineasFilter.Contains(i.Id)).ToList();
            // Si el filtro abarca TODOS los artículos de bar → primer envío completo
            bool esPrimerEnvio = items.All(i => lineasFilter.Contains(i.Id));
            titulo = esPrimerEnvio ? "COMANDA DE BAR" : "PEDIDO EXTRA - BAR";
        }
        else
        {
            bool tieneEnPreparacion = items.Any(i => i.Estado == "En Preparación" || i.Estado == "Pendiente");
            bool tieneListosOEntregados = items.Any(i => i.Estado == "Listo" || i.Estado == "Entregado");

            if (tieneEnPreparacion && tieneListosOEntregados)
            {
                titulo = "PEDIDO EXTRA - BAR";
                itemsParaImprimir = items.Where(i => i.Estado == "En Preparación" || i.Estado == "Pendiente").ToList();
            }
            else if (!tieneEnPreparacion)
            {
                titulo = "REIMPRESION BAR";
                itemsParaImprimir = items.ToList();
            }
            else
            {
                itemsParaImprimir = items.ToList();
            }
        }

        var esc = ConstruirCabeceraCocinaBar(titulo, orden.Numero, orden, orden.FechaCreacion);

        bool first = true;
        foreach (var item in itemsParaImprimir)
        {
            if (first)
            {
                esc.PrintLine("+----------------------------------------------+");
                first = false;
            }

            var prodNombre = (item.Servicio?.Nombre ?? "Producto").ToUpper();
            PrintCardLine(esc, $"[ {item.Cantidad} ]  {prodNombre}", "");

            var opciones = StringFragmentOpcionesLinea(item);
            if (!string.IsNullOrEmpty(opciones))
            {
                PrintCardLine(esc, $"--> {opciones}", "       ");
            }

            if (!string.IsNullOrEmpty(item.Notas))
            {
                PrintCardLine(esc, $"(¡) NOTA: {item.Notas}", "       ");
            }

            esc.PrintLine("+----------------------------------------------+");
        }

        if (!string.IsNullOrEmpty(orden.Observaciones))
        {
            if (first)
            {
                esc.PrintLine("+----------------------------------------------+");
            }
            PrintCardLine(esc, $"OBS: {orden.Observaciones}", "");
            esc.PrintLine("+----------------------------------------------+");
        }

        ConstruirPiePagina(esc, "¡Buen servicio!");
        return esc;
    }

    public byte[] GenerarTicketCocina(Factura orden, List<int>? lineasFilter = null) => BuildTicketCocina(orden, lineasFilter).GetBytes();
    public byte[] GenerarTicketBar(Factura orden, List<int>? lineasFilter = null) => BuildTicketBar(orden, lineasFilter).GetBytes();

    private EscPosBuilder ConstruirCabeceraDelivery(string numero, Factura orden, DateTime fecha)
    {
        var esc = new EscPosBuilder();
        var nombreRest = ObtenerNombreRestaurante();
        var logoPath = ObtenerLogoFisico();

        if (!string.IsNullOrEmpty(logoPath))
            esc.PrintImage(logoPath);

        esc.AlignCenter()
           .DoubleSizeFont()
           .BoldOn()
           .PrintLine(nombreRest)
           .NormalFont()
           .BoldOff();

        var direccion = ObtenerDireccionRestaurante();
        if (!string.IsNullOrEmpty(direccion))
            esc.PrintLine(direccion);

        var telefono = ObtenerTelefonoRestaurante();
        if (!string.IsNullOrEmpty(telefono))
            esc.PrintLine($"TEL: {telefono}");

        var ruc = ObtenerRucRestaurante();
        if (!string.IsNullOrEmpty(ruc))
            esc.PrintLine($"RUC: {ruc}");

          esc.DrawDivider()
             .AlignCenter()
             .BoldOn()
             .PrintLine("** DELIVERY **")
             .NormalFont()
             .BoldOff()
             .AlignLeft()
             .PrintLine($"ORDEN:   #{numero}")
             .PrintLine($"FECHA:   {fecha:dd/MM/yyyy HH:mm}")
             .BoldOn();

          // Solo imprimir si tiene nombre, igual que TEL y DIR
          if (!string.IsNullOrWhiteSpace(orden.DeliveryClienteNombre))
          {
              esc.PrintLine($"CLIENTE: {orden.DeliveryClienteNombre}");
          }
          esc.BoldOff();

          if (!string.IsNullOrWhiteSpace(orden.DeliveryClienteTelefono))
              esc.PrintLine($"TEL:     {orden.DeliveryClienteTelefono}");
              
          if (!string.IsNullOrWhiteSpace(orden.DeliveryClienteDireccion))
              esc.PrintLine($"DIR:     {orden.DeliveryClienteDireccion}");

          esc.DrawDivider();
          
          return esc;
    }

    private EscPosBuilder BuildRecibo(Pago pago, Factura orden)
    {
        var esDelivery = string.Equals(orden.OrigenPedido, SD.OrigenPedidoDelivery, StringComparison.OrdinalIgnoreCase);
        var esc = esDelivery
            ? ConstruirCabeceraDelivery(orden.Numero, orden, pago.FechaPago)
            : ConstruirCabecera("RECIBO", orden.Numero, orden, pago.FechaPago);

        esc.BoldOn()
           .Print3Columns("CANT", "PRODUCTO", "PRECIO")
           .BoldOff()
           .DrawDivider();

        var lineasAgrupadas = orden.FacturaServicios
            .GroupBy(item => new { 
                item.ServicioId, 
                NotasStr = (item.Notas ?? "").Trim(),
                OpcionesStr = StringFragmentOpcionesLinea(item) 
            })
            .Select(g => new {
                Cantidad = g.Sum(x => x.Cantidad),
                Nombre = g.First().Servicio?.Nombre ?? "Producto",
                Monto = g.Sum(x => x.Monto),
                Opciones = g.Key.OpcionesStr
            });

        foreach (var item in lineasAgrupadas)
        {
            esc.Print3Columns(item.Cantidad.ToString(), item.Nombre, $"C${item.Monto:N2}");

            if (!string.IsNullOrEmpty(item.Opciones))
                esc.PrintLine($"   · {item.Opciones}");
        }

        esc.DrawDivider()
           .PrintColumns("SUBTOTAL:", $"C${orden.Monto:N2}");

        if (pago.DescuentoMonto > 0.005m)
            esc.PrintColumns("DESCUENTO:", $"-C${pago.DescuentoMonto:N2}");
        
        esc.DrawDivider()
           .BoldOn()
           .DoubleSizeFont()
           .PrintColumns("TOTAL:", $"C${pago.Monto:N2}")
           .NormalFont()
           .BoldOff()
           .DrawDivider();

        string monedaSimbolo = pago.Moneda == "USD" ? "$" : "C$";
        string tipoPagoTexto = string.IsNullOrWhiteSpace(pago.TipoPago) ? "Efectivo" : pago.TipoPago;
        esc.PrintColumns($"PAGO CON: {tipoPagoTexto}", $"{monedaSimbolo}{pago.MontoRecibido:N2}")
           .PrintColumns("VUELTO:", $"C${pago.Vuelto:N2}")
           .DrawDivider();

        ConstruirPiePagina(esc, esDelivery ? "¡Gracias por su pedido!" : "¡Gracias por su visita!");
        return esc;
    }

    public byte[] GenerarTicketRecibo(Pago pago, Factura orden)
    {
        var esc = BuildRecibo(pago, orden);
        esc.OpenDrawer(); 
        return esc.GetBytes();
    }

    public string GenerarPreviewRecibo(Pago pago, Factura orden)
    {
        return BuildRecibo(pago, orden).GetPlainText();
    }

    private EscPosBuilder BuildComanda(Factura orden)
    {
        var esDelivery = string.Equals(orden.OrigenPedido, SD.OrigenPedidoDelivery, StringComparison.OrdinalIgnoreCase);
        var esc = esDelivery
            ? ConstruirCabeceraDelivery(orden.Numero, orden, orden.FechaCreacion)
            : ConstruirCabecera("COMANDA", orden.Numero, orden, orden.FechaCreacion);

        esc.BoldOn()
           .Print3Columns("CANT", "PRODUCTO", "PRECIO")
           .BoldOff()
           .DrawDivider();

        var lineasAgrupadas = orden.FacturaServicios
            .GroupBy(item => new { 
                item.ServicioId, 
                NotasStr = (item.Notas ?? "").Trim(),
                OpcionesStr = StringFragmentOpcionesLinea(item) 
            })
            .Select(g => new {
                Cantidad = g.Sum(x => x.Cantidad),
                Nombre = g.First().Servicio?.Nombre ?? "Producto",
                Monto = g.Sum(x => x.Monto),
                Opciones = g.Key.OpcionesStr,
                Notas = g.Key.NotasStr
            });

        foreach (var item in lineasAgrupadas)
        {
            esc.Print3Columns(item.Cantidad.ToString(), item.Nombre, $"C${item.Monto:N2}");

            if (!string.IsNullOrEmpty(item.Opciones))
                esc.PrintLine($"   · {item.Opciones}");
            if (!string.IsNullOrEmpty(item.Notas))
                esc.PrintLine($"   [!] {item.Notas}");
        }

        esc.DrawDivider()
           .BoldOn()
           .DoubleSizeFont()
           .PrintColumns("TOTAL:", $"C${orden.Monto:N2}")
           .NormalFont()
           .BoldOff()
           .DrawDivider();

        ConstruirPiePagina(esc, esDelivery ? "Comanda Delivery" : "Comanda para mesero");
        return esc;
    }

    public byte[] GenerarTicketComanda(Factura orden) => BuildComanda(orden).GetBytes();
    public string GenerarPreviewComanda(Factura orden)
    {
        return BuildComanda(orden).GetPlainText();
    }

    public string GenerarPreviewCocina(Factura orden, List<int>? lineasFilter = null)
    {
        return BuildTicketCocina(orden, lineasFilter).GetPlainText();
    }

    public string GenerarPreviewBar(Factura orden, List<int>? lineasFilter = null)
    {
        return BuildTicketBar(orden, lineasFilter).GetPlainText();
    }

    private EscPosBuilder BuildTicketCorte(CierreCaja cierre)
    {
        var esc = new EscPosBuilder();
        var nombreRest = ObtenerNombreRestaurante();
        var logoPath = ObtenerLogoFisico();

        if (!string.IsNullOrEmpty(logoPath))
        {
            esc.PrintImage(logoPath);
        }

        esc.AlignCenter()
           .DoubleSizeFont()
           .BoldOn()
           .PrintLine(nombreRest)
           .NormalFont()
           .BoldOff();

        var direccion = ObtenerDireccionRestaurante();
        if (!string.IsNullOrEmpty(direccion))
        {
            esc.PrintLine(direccion);
        }

        var telefono = ObtenerTelefonoRestaurante();
        if (!string.IsNullOrEmpty(telefono))
        {
            esc.PrintLine($"TEL: {telefono}");
        }

        var ruc = ObtenerRucRestaurante();
        if (!string.IsNullOrEmpty(ruc))
        {
            esc.PrintLine($"RUC: {ruc}");
        }

        esc.DrawDivider()
           .AlignCenter()
           .BoldOn()
           .PrintLine("CORTE DE CAJA")
           .BoldOff()
           .DrawDivider()
           .AlignLeft()
           .PrintLine($"CAJERO: {cierre.Usuario?.NombreCompleto ?? "Sin registro"}")
           .DrawDivider()
           .PrintColumns("FONDO INICIAL:", $"C$ {cierre.MontoInicial ?? 0:N2}")
           .PrintColumns("TOTAL VENTAS (INGRESOS):", $"C$ {cierre.TotalGeneral:N2}")
           // Propinas can't be easily deduced from CierreCaja without further properties. Skipping or adding as 0 if not tracked natively here.
           // Total propinas no está mapeado directamente en CierreCaja. Omitimos.
           .DrawDivider()
           .PrintColumns("EFECTIVO EN CAJA:", $"C$ {cierre.TotalEfectivo:N2}")
           .PrintColumns("TARJETA:", $"C$ {cierre.TotalTarjeta:N2}");
           
        if (cierre.TotalTransferencia > 0)
        {
            esc.PrintColumns("TRANSFERENCIA:", $"C$ {cierre.TotalTransferencia:N2}");
        }

        esc.DrawDivider()
           .PrintColumns("TOTAL ESPERADO:", $"C$ {cierre.MontoEsperado:N2}")
           .PrintColumns("TOTAL DECLARADO:", $"C$ {cierre.MontoReal ?? 0:N2}");

        var difStr = cierre.Diferencia.HasValue ? $"C$ {cierre.Diferencia.Value:N2}" : "N/D";
        esc.PrintColumns("DIFERENCIA:", difStr)
           .DrawDivider()
           .AlignCenter()
           .PrintLine("FIN DEL REPORTE")
           .PrintLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"))
           .FeedLines(4)
           .CutPaper();

        return esc;
    }

    public byte[] GenerarTicketCorte(CierreCaja cierre) => BuildTicketCorte(cierre).GetBytes();
    
    public string GenerarPreviewCorte(CierreCaja cierre)
    {
        return BuildTicketCorte(cierre).GetPlainText();
    }

    public byte[] GenerarTicketCancelacionItem(Factura orden, FacturaServicio itemCancelado)
    {
        var esc = new EscPosBuilder();
        
        bool esCocina = CocinaCatalogoHelper.FacturaServicioRequiereCocina(itemCancelado);
        string titulo = esCocina ? "CANCELACION DE COCINA" : "CANCELACION DE BAR";

        esc.AlignCenter()
           .BoldOn()
           .PrintLine("================================================")
           .PrintLine($"** {titulo} **")
           .PrintLine("================================================")
           .NormalFont()
           .BoldOff()
           .AlignLeft();

        string mesaStr;
        if (string.Equals(orden.OrigenPedido, SD.OrigenPedidoDelivery, StringComparison.OrdinalIgnoreCase))
        {
            mesaStr = "ORIGEN: Delivery";
        }
        else if (!string.IsNullOrEmpty(orden.OrigenPedido) && orden.OrigenPedido.Trim().ToLower() != "salon")
        {
            mesaStr = $"ORIGEN: {orden.OrigenPedido}";
        }
        else
        {
            mesaStr = $"ORIGEN: {orden.Mesa?.Numero ?? "S/M"}";
        }
        
        var ordenStr = $"ORDEN: #{orden.Numero}";
        var meseroStr = $"MESERO: {orden.Mesero?.NombreCompleto ?? "Sin registro"}";

        esc.PrintLine(ordenStr);
        esc.PrintLine(mesaStr);
        esc.PrintLine(meseroStr);

        if (string.Equals(orden.OrigenPedido, SD.OrigenPedidoDelivery, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(orden.DeliveryClienteNombre))
            {
                esc.BoldOn()
                   .PrintLine($"CLIENTE: {orden.DeliveryClienteNombre}")
                   .BoldOff();
            }
            if (!string.IsNullOrWhiteSpace(orden.DeliveryClienteTelefono))
            {
                esc.PrintLine($"TEL:     {orden.DeliveryClienteTelefono}");
            }
            if (!string.IsNullOrWhiteSpace(orden.DeliveryClienteDireccion))
            {
                esc.PrintLine($"DIR:     {orden.DeliveryClienteDireccion}");
            }
        }

        esc.PrintLine("================================================");
        esc.PrintLine("+----------------------------------------------+");
        
        var prodNombre = (itemCancelado.Servicio?.Nombre ?? "Producto").ToUpper();
        
        esc.BoldOn();
        PrintCardLine(esc, $"❌ [ {itemCancelado.Cantidad} ]  {prodNombre}", "");
        PrintCardLine(esc, "      PRODUCTO CANCELADO", "      ");
        esc.BoldOff();

        var opciones = StringFragmentOpcionesLinea(itemCancelado);
        if (!string.IsNullOrEmpty(opciones))
        {
            PrintCardLine(esc, $"--> {opciones}", "       ");
        }

        if (!string.IsNullOrEmpty(itemCancelado.Notas))
        {
            PrintCardLine(esc, $"(¡) NOTA: {itemCancelado.Notas}", "       ");
        }

        esc.PrintLine("+----------------------------------------------+");
        
        ConstruirPiePagina(esc, "¡Aviso de cancelación!");
        return esc.GetBytes();
    }
}
