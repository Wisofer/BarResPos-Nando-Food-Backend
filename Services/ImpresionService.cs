using System;
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

        return esc.AlignCenter()
           .DoubleSizeFont()
           .BoldOn()
           .PrintLine(nombreRest)
           .NormalFont()
           .BoldOff()
           .DrawDivider()
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

    public byte[] GenerarTicketCocina(Factura orden)
    {
        var esc = ConstruirCabecera("ORDEN", orden.Numero, orden, orden.FechaCreacion);

        esc.BoldOn()
           .Print3Columns("CANT", "PRODUCTO", "NOTAS")
           .BoldOff()
           .DrawDivider();

        foreach (var item in CocinaCatalogoHelper.LineasCocina(orden.FacturaServicios))
        {
            esc.BoldOn()
               .PrintLine($"{item.Cantidad.ToString().PadRight(4)} {item.Servicio.Nombre}")
               .BoldOff();

            var opciones = StringFragmentOpcionesLinea(item);
            if (!string.IsNullOrEmpty(opciones))
                esc.PrintLine($"   · {opciones}");
            if (!string.IsNullOrEmpty(item.Notas))
                esc.PrintLine($"   [!] {item.Notas}");
        }

        esc.DrawDivider();

        if (!string.IsNullOrEmpty(orden.Observaciones))
        {
            esc.BoldOn()
               .PrintLine("OBSERVACIONES:")
               .BoldOff()
               .PrintLine(orden.Observaciones)
               .DrawDivider();
        }

        ConstruirPiePagina(esc, "¡Gracias por su trabajo!");
        return esc.GetBytes();
    }

    public byte[] GenerarTicketBar(Factura orden)
    {
        var esc = ConstruirCabecera("TICKET BAR", orden.Numero, orden, orden.FechaCreacion);

        esc.BoldOn()
           .Print3Columns("CANT", "PRODUCTO", "NOTAS")
           .BoldOff()
           .DrawDivider();

        foreach (var item in CocinaCatalogoHelper.LineasBar(orden.FacturaServicios))
        {
            esc.BoldOn()
               .PrintLine($"{item.Cantidad.ToString().PadRight(4)} {item.Servicio.Nombre}")
               .BoldOff();

            var opciones = StringFragmentOpcionesLinea(item);
            if (!string.IsNullOrEmpty(opciones))
                esc.PrintLine($"   · {opciones}");
            if (!string.IsNullOrEmpty(item.Notas))
                esc.PrintLine($"   [!] {item.Notas}");
        }

        esc.DrawDivider();

        if (!string.IsNullOrEmpty(orden.Observaciones))
        {
            esc.BoldOn()
               .PrintLine("OBSERVACIONES:")
               .BoldOff()
               .PrintLine(orden.Observaciones)
               .DrawDivider();
        }

        ConstruirPiePagina(esc, "¡Buen servicio!");
        return esc.GetBytes();
    }

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
}
