using System.Net;
using System.Linq;
using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

/// <summary>
/// Servicio para generar tickets de impresión térmica
/// </summary>
public class ImpresionService : IImpresionService
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public ImpresionService(IConfiguration configuration, ApplicationDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }
    private const string NOMBRE_RESTAURANTE = "Bar Rest POS";

    /// <summary>Resuelve el logo desde la base de datos o appsettings.json y retorna el tag img.</summary>
    private string BuildLogoImgTag()
    {
        var url = _context.Configuraciones
            .AsNoTracking()
            .Where(c => c.Clave == "Tickets:LogoUrl")
            .Select(c => c.Valor)
            .FirstOrDefault()?.Trim();

        if (string.IsNullOrEmpty(url))
        {
            url = _configuration["Tickets:LogoUrl"]?.Trim();
        }

        if (string.IsNullOrEmpty(url))
            return "";

        if (url.StartsWith("/"))
        {
            var baseUrl = _configuration["App:PublicBaseUrl"]?.Trim();
            if (string.IsNullOrEmpty(baseUrl))
            {
                baseUrl = "http://localhost:5000";
            }
            url = baseUrl.TrimEnd('/') + url;
        }

        return $"<img src=\"{WebUtility.HtmlEncode(url)}\" alt=\"Logo\" class=\"logo\" />";
    }

    public string GenerarTicketCocina(Factura orden)
    {
        var logoHtml = BuildLogoImgTag();
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Ticket Cocina - {orden.Numero}</title>
    <style>
        {GetTicketStyles()}
    </style>
</head>
<body>
    <div class='ticket'>
        <div class='header'>
            {logoHtml}
            <h1 class='nombre-restaurante'>{NOMBRE_RESTAURANTE}</h1>
            <div class='divider'></div>
        </div>
        
        <div class='info-orden'>
            <div class='line'>
                <span class='label'>ORDEN:</span>
                <span class='value'>{orden.Numero}</span>
            </div>
            <div class='line'>
                <span class='label'>MESA:</span>
                <span class='value'>{orden.Mesa?.Numero ?? "S/M"}</span>
            </div>
            <div class='line'>
                <span class='label'>MESERO:</span>
                <span class='value'>{orden.Mesero?.NombreCompleto ?? "N/A"}</span>
            </div>
            <div class='line'>
                <span class='label'>FECHA:</span>
                <span class='value'>{orden.FechaCreacion:dd/MM/yyyy HH:mm}</span>
            </div>
        </div>
        
        <div class='divider'></div>
        
        <div class='productos'>
            <div class='productos-header'>
                <span>CANT</span>
                <span>PRODUCTO</span>
                <span>NOTAS</span>
            </div>";

        foreach (var item in CocinaCatalogoHelper.LineasCocina(orden.FacturaServicios))
        {
            var opcionesHtml = HtmlFragmentOpcionesLinea(item);
            html += $@"
            <div class='producto-item'>
                <span class='cantidad'>{item.Cantidad}</span>
                <div class='producto-info'>
                    <span class='producto-nombre'>{item.Servicio.Nombre}</span>
                    {opcionesHtml}
                    {(!string.IsNullOrEmpty(item.Notas) ? $"<span class='notas'>📝 {WebUtility.HtmlEncode(item.Notas)}</span>" : "")}
                </div>
            </div>";
        }

        html += $@"
        </div>
        
        <div class='divider'></div>
        
        {(!string.IsNullOrEmpty(orden.Observaciones) ? $@"
        <div class='observaciones'>
            <strong>OBSERVACIONES:</strong>
            <p>{orden.Observaciones}</p>
        </div>
        <div class='divider'></div>
        " : "")}
        
        <div class='footer'>
            <p class='mensaje'>¡Gracias por su trabajo!</p>
            <p class='fecha-impresion'>{DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
        </div>
    </div>
</body>
</html>";

        return html;
    }

    public string GenerarTicketRecibo(Pago pago, Factura orden)
    {
        var logoHtml = BuildLogoImgTag();
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Recibo - {orden.Numero}</title>
    <style>
        {GetTicketStyles()}
    </style>
</head>
<body>
    <div class='ticket ticket-recibo'>
        <div class='header'>
            {logoHtml}
            <h1 class='nombre-restaurante'>{NOMBRE_RESTAURANTE}</h1>
            <div class='divider'></div>
        </div>
        
        <div class='info-orden'>
            <div class='line'>
                <span class='label'>RECIBO:</span>
                <span class='value'>{orden.Numero}</span>
            </div>
            <div class='line'>
                <span class='label'>MESA:</span>
                <span class='value'>{orden.Mesa?.Numero ?? "S/M"}</span>
            </div>
            <div class='line'>
                <span class='label'>FECHA:</span>
                <span class='value'>{pago.FechaPago:dd/MM/yyyy HH:mm}</span>
            </div>
        </div>
        
        <div class='divider'></div>
        
        <div class='productos'>
            <div class='productos-header'>
                <span>CANT</span>
                <span>PRODUCTO</span>
                <span>PRECIO</span>
            </div>";

        foreach (var item in orden.FacturaServicios)
        {
            var opcionesHtml = HtmlFragmentOpcionesLinea(item);
            html += $@"
            <div class='producto-item'>
                <span class='cantidad'>{item.Cantidad}</span>
                <div class='producto-info'>
                    <span class='producto-nombre'>{item.Servicio.Nombre}</span>
                    {opcionesHtml}
                </div>
                <span class='precio'>C${item.Monto:N2}</span>
            </div>";
        }

        var lineaDescuento = pago.DescuentoMonto > 0.005m
            ? $@"
            <div class='total-line'>
                <span>DESCUENTO:</span>
                <span>-C${pago.DescuentoMonto:N2}</span>
            </div>"
            : "";

        html += $@"
        </div>
        
        <div class='divider'></div>
        
        <div class='totales totales-recibo'>
            <div class='total-line'>
                <span>SUBTOTAL:</span>
                <span>C${orden.Monto:N2}</span>
            </div>{lineaDescuento}
            <div class='total-line total-final'>
                <span>TOTAL PAGADO:</span>
                <span>C${pago.Monto:N2}</span>
            </div>
        </div>
        
        <div class='divider'></div>
        
        <div class='footer footer-recibo'>
            <p class='mensaje'>¡Gracias por su visita!</p>
            <p class='fecha-impresion'>{DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
        </div>
    </div>
    <script>
        // Auto-print idempotente por pagoId: algunos navegadores/iframes pueden disparar el evento de carga más de una vez.
        (function () {{
            try {{
                window.__barrest_ticketPrintDone = window.__barrest_ticketPrintDone || {{}};
                var key = 'recibo-{pago.Id}';
                if (window.__barrest_ticketPrintDone[key]) return;
                window.__barrest_ticketPrintDone[key] = true;
            }} catch (e) {{}}

            const runPrint = () => {{
                try {{
                    window.print();
                }} catch (e) {{}}
            }};

            if (document.readyState === 'complete') {{
                setTimeout(runPrint, 100);
            }} else {{
                window.addEventListener('load', function () {{
                    runPrint();
                }}, {{ once: true }});
            }}
        }})();
    </script>
</body>
</html>";

        return html;
    }

    public string GenerarTicketComanda(Factura orden)
    {
        var logoHtml = BuildLogoImgTag();
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Comanda - {orden.Numero}</title>
    <style>
        {GetTicketStyles()}
    </style>
</head>
<body>
    <div class='ticket'>
        <div class='header'>
            {logoHtml}
            <h1 class='nombre-restaurante'>{NOMBRE_RESTAURANTE}</h1>
            <div class='divider'></div>
        </div>
        
        <div class='info-orden'>
            <div class='line'>
                <span class='label'>COMANDA:</span>
                <span class='value'>{orden.Numero}</span>
            </div>
            <div class='line'>
                <span class='label'>MESA:</span>
                <span class='value'>{orden.Mesa?.Numero ?? "S/M"}</span>
            </div>
            <div class='line'>
                <span class='label'>MESERO:</span>
                <span class='value'>{orden.Mesero?.NombreCompleto ?? "N/A"}</span>
            </div>
            <div class='line'>
                <span class='label'>FECHA:</span>
                <span class='value'>{orden.FechaCreacion:dd/MM/yyyy HH:mm}</span>
            </div>
        </div>
        
        <div class='divider'></div>
        
        <div class='productos'>
            <div class='productos-header'>
                <span>CANT</span>
                <span>PRODUCTO</span>
                <span>PRECIO</span>
            </div>";

        foreach (var item in orden.FacturaServicios)
        {
            var opcionesHtml = HtmlFragmentOpcionesLinea(item);
            html += $@"
            <div class='producto-item'>
                <span class='cantidad'>{item.Cantidad}</span>
                <div class='producto-info'>
                    <span class='producto-nombre'>{item.Servicio.Nombre}</span>
                    {opcionesHtml}
                    {(!string.IsNullOrEmpty(item.Notas) ? $"<span class='notas'>📝 {WebUtility.HtmlEncode(item.Notas)}</span>" : "")}
                </div>
                <span class='precio'>C${item.Monto:N2}</span>
            </div>";
        }

        html += $@"
        </div>
        
        <div class='divider'></div>
        
        <div class='totales'>
            <div class='total-line total-final'>
                <span>TOTAL:</span>
                <span>C${orden.Monto:N2}</span>
            </div>
        </div>
        
        <div class='divider'></div>
        
        <div class='footer'>
            <p class='mensaje'>Comanda para mesero</p>
            <p class='fecha-impresion'>{DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
        </div>
    </div>
    <script>
        // Auto-print idempotente por ordenId (delivery/salón): evita doble impresión si el iframe dispara load duplicado.
        (function () {{
            try {{
                window.__barrest_ticketPrintDone = window.__barrest_ticketPrintDone || {{}};
                var key = 'comanda-{orden.Id}';
                if (window.__barrest_ticketPrintDone[key]) return;
                window.__barrest_ticketPrintDone[key] = true;
            }} catch (e) {{}}

            const runPrint = () => {{
                try {{
                    window.print();
                }} catch (e) {{}}
            }};

            if (document.readyState === 'complete') {{
                setTimeout(runPrint, 100);
            }} else {{
                window.addEventListener('load', function () {{
                    runPrint();
                }}, {{ once: true }});
            }}
        }})();
    </script>
</body>
</html>";

        return html;
    }

    private string GetTicketStyles()
    {
        return @"
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        
        body {
            font-family: 'Courier New', monospace;
            font-size: 12px;
            line-height: 1.4;
            background: white;
            padding: 10px;
        }
        
        .ticket {
            width: 80mm;
            max-width: 80mm;
            margin: 0 auto;
            background: white;
            padding: 10px;
        }
        
        .header {
            text-align: center;
            margin-bottom: 10px;
        }
        
        .logo {
            max-width: 140px;
            max-height: 60px;
            width: auto;
            height: auto;
            object-fit: contain;
            margin: 0 auto 8px;
            display: block;
        }
        
        .nombre-restaurante {
            font-size: 14px;
            font-weight: bold;
            text-transform: uppercase;
            margin: 8px 0;
            line-height: 1.3;
        }
        
        .divider {
            border-top: 1px dashed #000;
            margin: 10px 0;
        }
        
        .info-orden {
            margin: 10px 0;
        }
        
        .line {
            display: flex;
            justify-content: space-between;
            margin: 4px 0;
            font-size: 11px;
        }
        
        .label {
            font-weight: bold;
        }
        
        .value {
            text-align: right;
        }
        
        .productos {
            margin: 10px 0;
            text-align: left;
        }
        
        .productos-header {
            display: flex;
            align-items: flex-end;
            gap: 6px;
            font-weight: bold;
            font-size: 10px;
            margin-bottom: 8px;
            border-bottom: 1px solid #000;
            padding-bottom: 4px;
        }
        
        .productos-header span:nth-child(1) {
            flex: 0 0 28px;
            text-align: left;
        }
        
        .productos-header span:nth-child(2) {
            flex: 1 1 auto;
            min-width: 0;
            text-align: left;
        }
        
        .productos-header span:nth-child(3) {
            flex: 0 0 58px;
            text-align: right;
        }
        
        .producto-item {
            display: flex;
            align-items: flex-start;
            gap: 6px;
            margin: 6px 0;
            font-size: 11px;
        }
        
        .cantidad {
            font-weight: bold;
            flex: 0 0 28px;
            text-align: left;
        }
        
        .producto-info {
            flex: 1 1 auto;
            min-width: 0;
            margin: 0;
            text-align: left;
        }
        
        .producto-nombre {
            display: block;
            font-weight: bold;
            text-align: left;
            word-wrap: break-word;
        }
        
        .notas {
            display: block;
            font-size: 9px;
            color: #666;
            margin-top: 2px;
        }
        
        .precio {
            flex: 0 0 58px;
            text-align: right;
        }
        
        .totales {
            margin: 10px 0;
            text-align: left;
        }
        
        .totales-recibo .total-line span:first-child {
            text-align: left;
        }
        
        .total-line {
            display: flex;
            justify-content: space-between;
            align-items: baseline;
            gap: 8px;
            margin: 6px 0;
            font-size: 11px;
            text-align: left;
        }
        
        .total-line span:first-child {
            flex: 0 1 auto;
            text-align: left;
        }
        
        .total-line span:last-child {
            flex: 0 0 auto;
            text-align: right;
            white-space: nowrap;
        }
        
        .total-line.vuelto {
            color: #0066cc;
            font-weight: bold;
        }
        
        .total-line.total-final {
            font-size: 14px;
            font-weight: bold;
            border-top: 2px solid #000;
            padding-top: 6px;
            margin-top: 8px;
        }
        
        .observaciones {
            margin: 10px 0;
            font-size: 10px;
        }
        
        .observaciones strong {
            display: block;
            margin-bottom: 4px;
        }
        
        .footer {
            text-align: center;
            margin-top: 15px;
        }
        
        .footer-recibo {
            text-align: center;
            margin-top: 12px;
        }
        
        .footer-recibo .mensaje {
            font-size: 12px;
            font-weight: bold;
            margin: 6px 0 4px 0;
        }
        
        .footer-recibo .fecha-impresion {
            font-size: 9px;
            color: #444;
        }
        
        .mensaje {
            font-size: 11px;
            margin: 8px 0;
        }
        
        .fecha-impresion {
            font-size: 9px;
            color: #666;
        }
        
        @media print {
            body {
                padding: 0;
            }
            
            .ticket {
                padding: 5px;
            }
            
            @page {
                size: 80mm auto;
                margin: 0;
            }
        }";
    }

    private static string HtmlFragmentOpcionesLinea(FacturaServicio item)
    {
        var opts = item.OpcionesSeleccionadas;
        if (opts == null || opts.Count == 0) return "";
        // En tickets queremos solo las opciones seleccionadas (sin el prefijo "Grupo:").
        // Ej: en lugar de "Opciones especiales: Mango · Habanero" -> "Mango · Habanero".
        var r = string.Join(" · ",
            opts
                .OrderBy(o => o.NombreGrupo)
                .ThenBy(o => o.NombreOpcion)
                .Select(o => o.NombreOpcion)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList());

        return string.IsNullOrEmpty(r)
            ? ""
            : $"<span class='notas opciones'>○ {WebUtility.HtmlEncode(r)}</span>";
    }
}

