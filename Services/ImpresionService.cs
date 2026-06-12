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

        return esc.DrawDivider()
           .AlignLeft()
           .BoldOn()
           .PrintLine($"{tipoTicket}: {numero}")
           .BoldOff()
           .PrintLine($"MESA:   {orden.Mesa?.Numero ?? "S/M"}")
           .PrintLine($"MESERO: {orden.Mesero?.NombreCompleto ?? "N/A"}")
           .PrintLine($"FECHA:  {fecha:dd/MM/yyyy HH:mm}")
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

        var ubicacionNombre = orden.Mesa?.Ubicacion?.Nombre?.ToUpper() ?? "";
        var mesaStr = $"MESA: {orden.Mesa?.Numero ?? "S/M"}";
        if (!string.IsNullOrEmpty(ubicacionNombre))
        {
            mesaStr += $" [{ubicacionNombre}]";
        }
        var ordenStr = $"ORDEN: #{numero}";

        var horaStr = $"HORA: {fecha:HH:mm (dd/MM)}";
        var meseroStr = $"MESERO: {orden.Mesero?.NombreCompleto ?? "N/A"}";

        esc.PrintColumns(mesaStr, ordenStr);
        esc.PrintColumns(horaStr, meseroStr);
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
            titulo = "ADICION DE COCINA";
            itemsParaImprimir = items.Where(i => lineasFilter.Contains(i.Id)).ToList();
        }
        else
        {
            bool tieneEnPreparacion = items.Any(i => i.Estado == "En Preparación" || i.Estado == "Pendiente");
            bool tieneListosOEntregados = items.Any(i => i.Estado == "Listo" || i.Estado == "Entregado");

            if (tieneEnPreparacion && tieneListosOEntregados)
            {
                titulo = "ADICION DE COCINA";
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
            titulo = "ADICION DE BAR";
            itemsParaImprimir = items.Where(i => lineasFilter.Contains(i.Id)).ToList();
        }
        else
        {
            bool tieneEnPreparacion = items.Any(i => i.Estado == "En Preparación" || i.Estado == "Pendiente");
            bool tieneListosOEntregados = items.Any(i => i.Estado == "Listo" || i.Estado == "Entregado");

            if (tieneEnPreparacion && tieneListosOEntregados)
            {
                titulo = "ADICION DE BAR";
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

    private EscPosBuilder BuildRecibo(Pago pago, Factura orden)
    {
        var esc = ConstruirCabecera("RECIBO", orden.Numero, orden, pago.FechaPago);

        esc.BoldOn()
           .Print3Columns("CANT", "PRODUCTO", "PRECIO")
           .BoldOff()
           .DrawDivider();

        foreach (var item in orden.FacturaServicios)
        {
            esc.BoldOn()
               .Print3Columns(item.Cantidad.ToString(), item.Servicio.Nombre, $"C${item.Monto:N2}")
               .BoldOff();

            var opciones = StringFragmentOpcionesLinea(item);
            if (!string.IsNullOrEmpty(opciones))
                esc.PrintLine($"   · {opciones}");
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

        ConstruirPiePagina(esc, "¡Gracias por su visita!");
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
        var esc = ConstruirCabecera("COMANDA", orden.Numero, orden, orden.FechaCreacion);

        esc.BoldOn()
           .Print3Columns("CANT", "PRODUCTO", "PRECIO")
           .BoldOff()
           .DrawDivider();

        foreach (var item in orden.FacturaServicios)
        {
            esc.BoldOn()
               .Print3Columns(item.Cantidad.ToString(), item.Servicio.Nombre, $"C${item.Monto:N2}")
               .BoldOff();

            var opciones = StringFragmentOpcionesLinea(item);
            if (!string.IsNullOrEmpty(opciones))
                esc.PrintLine($"   · {opciones}");
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

        ConstruirPiePagina(esc, "Comanda para mesero");
        return esc;
    }

    public byte[] GenerarTicketComanda(Factura orden) => BuildComanda(orden).GetBytes();
    public string GenerarPreviewComanda(Factura orden) => BuildComanda(orden).GetPlainText();
    public string GenerarPreviewCocina(Factura orden, List<int>? lineasFilter = null) => BuildTicketCocina(orden, lineasFilter).GetPlainText();
    public string GenerarPreviewBar(Factura orden, List<int>? lineasFilter = null) => BuildTicketBar(orden, lineasFilter).GetPlainText();
}
