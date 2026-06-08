using BarRestPOS.Models.Entities;

namespace BarRestPOS.Utils;

/// <summary>
/// Determina si una línea de pedido participa en el flujo KDS según la categoría del producto.
/// Sin categoría enlazada (FK nula): se asume que sí va a cocina (compatibilidad con datos antiguos).
/// </summary>
public static class CocinaCatalogoHelper
{
    public static bool ServicioRequiereCocina(Servicio? servicio)
    {
        if (servicio?.CategoriaProducto != null)
            return servicio.CategoriaProducto.RequiereCocina;
        return true;
    }

    public static bool FacturaServicioRequiereCocina(FacturaServicio? linea) =>
        linea != null && ServicioRequiereCocina(linea.Servicio);

    public static bool OrdenTieneLineasCocina(Factura orden) =>
        orden.FacturaServicios?.Any(FacturaServicioRequiereCocina) ?? false;

    public static IEnumerable<FacturaServicio> LineasCocina(IEnumerable<FacturaServicio> lineas) =>
        lineas.Where(FacturaServicioRequiereCocina);

    // BAR LOGIC
    public static bool ServicioRequiereBar(Servicio? servicio) =>
        !ServicioRequiereCocina(servicio);

    public static bool FacturaServicioRequiereBar(FacturaServicio? linea) =>
        linea != null && ServicioRequiereBar(linea.Servicio);

    public static bool OrdenTieneLineasBar(Factura orden) =>
        orden.FacturaServicios?.Any(FacturaServicioRequiereBar) ?? false;

    public static IEnumerable<FacturaServicio> LineasBar(IEnumerable<FacturaServicio> lineas) =>
        lineas.Where(FacturaServicioRequiereBar);

    /// <summary>
    /// True solo si hay al menos una línea de cocina y todas esas líneas están en estado Listo.
    /// </summary>
    public static bool TodasLasLineasCocinaEstanListas(Factura orden)
    {
        if (orden.FacturaServicios == null) return false;
        var cocina = LineasCocina(orden.FacturaServicios).ToList();
        return cocina.Count > 0 && cocina.All(fs => fs.Estado == SD.EstadoCocinaListo);
    }
}
