using BarRestPOS.Models.Entities;

namespace BarRestPOS.Utils;

public sealed record OpcionSeleccionDto(int GrupoId, int OpcionId);

/// <summary>
/// Valida selecciones contra catálogo y calcula precio unitario (base + adicionales).
/// </summary>
public static class ProductoOpcionesLineaHelper
{
    /// <summary>
    /// Grupos que aplican a validación: activos y con al menos una opción activa.
    /// </summary>
    public static List<ProductoOpcionGrupo> GruposEfectivos(IEnumerable<ProductoOpcionGrupo>? grupos) =>
        (grupos ?? [])
            .Where(g => g.Activo && g.Opciones != null && g.Opciones.Any(o => o.Activo))
            .OrderBy(g => g.Orden)
            .ToList();

    /// <summary>
    /// Valida y devuelve adicional por unidad y filas de persistencia (sin FacturaServicioId aún).
    /// </summary>
    public static (decimal adicionalPorUnidad, List<FacturaServicioOpcionSeleccion> filas, string? error) ValidarYConstruirFilas(
        Servicio producto,
        IReadOnlyList<OpcionSeleccionDto> selecciones)
    {
        var gruposEf = GruposEfectivos(producto.OpcionGrupos);
        var opcionesPorId = gruposEf
            .SelectMany(g => g.Opciones.Where(o => o.Activo).Select(o => (g, o)))
            .ToDictionary(x => x.o.Id, x => x);

        if (selecciones.Count == 0)
        {
            if (gruposEf.Count == 0)
                return (0m, new List<FacturaServicioOpcionSeleccion>(), null);

            foreach (var g in gruposEf)
            {
                var min = MinEfectivo(g);
                if (min > 0)
                    return (0, new List<FacturaServicioOpcionSeleccion>(),
                        $"El producto «{producto.Nombre}» requiere elegir opciones en «{g.Nombre}» (mínimo {min}).");
            }

            return (0m, new List<FacturaServicioOpcionSeleccion>(), null);
        }

        if (gruposEf.Count == 0)
            return (0, new List<FacturaServicioOpcionSeleccion>(),
                $"El producto «{producto.Nombre}» no tiene opciones configurables.");

        var seenOpcion = new HashSet<int>();
        var countPorGrupo = gruposEf.ToDictionary(g => g.Id, _ => 0);
        var filas = new List<FacturaServicioOpcionSeleccion>();
        decimal adicional = 0;

        foreach (var sel in selecciones)
        {
            if (!seenOpcion.Add(sel.OpcionId))
                return (0, new List<FacturaServicioOpcionSeleccion>(), "No se puede repetir la misma opción.");

            if (!opcionesPorId.TryGetValue(sel.OpcionId, out var pair))
                return (0, new List<FacturaServicioOpcionSeleccion>(), $"Opción inválida o inactiva (id {sel.OpcionId}).");

            var (grupo, opcion) = pair;
            if (grupo.Id != sel.GrupoId)
                return (0, new List<FacturaServicioOpcionSeleccion>(),
                    $"La opción {sel.OpcionId} no pertenece al grupo {sel.GrupoId}.");

            if (grupo.ServicioId != producto.Id)
                return (0, new List<FacturaServicioOpcionSeleccion>(), "La opción no corresponde a este producto.");

            countPorGrupo[grupo.Id]++;
            adicional += opcion.PrecioAdicional;
            filas.Add(new FacturaServicioOpcionSeleccion
            {
                ProductoOpcionGrupoId = grupo.Id,
                ProductoOpcionItemId = opcion.Id,
                NombreGrupo = grupo.Nombre,
                NombreOpcion = opcion.Nombre,
                PrecioAdicional = opcion.PrecioAdicional
            });
        }

        foreach (var g in gruposEf)
        {
            var n = countPorGrupo[g.Id];
            var min = MinEfectivo(g);
            var max = MaxEfectivo(g);
            if (n < min || n > max)
            {
                return (0, new List<FacturaServicioOpcionSeleccion>(),
                    $"En «{g.Nombre}» debe elegir entre {min} y {(max == int.MaxValue ? "varias" : max.ToString())} opción(es); seleccionó {n}.");
            }
        }

        adicional = Math.Round(adicional, 2, MidpointRounding.AwayFromZero);
        return (adicional, filas, null);
    }

    public static int MinEfectivo(ProductoOpcionGrupo g)
    {
        if (g.Obligatorio && g.MinSeleccion <= 0) return 1;
        return Math.Max(0, g.MinSeleccion);
    }

    public static int MaxEfectivo(ProductoOpcionGrupo g) =>
        g.MaxSeleccion <= 0 ? int.MaxValue : g.MaxSeleccion;

    public static string OpcionesResumen(IEnumerable<FacturaServicioOpcionSeleccion>? selecciones)
    {
        if (selecciones == null) return string.Empty;
        var list = selecciones
            .OrderBy(s => s.NombreGrupo)
            .ThenBy(s => s.NombreOpcion)
            .Select(s => $"{s.NombreGrupo}: {s.NombreOpcion}")
            .ToList();
        return list.Count == 0 ? string.Empty : string.Join(" · ", list);
    }

    /// <summary>Payload para catálogo POS / GET productos (solo grupos y opciones activos con al menos una opción visible).</summary>
    public static object[] MapOpcionesGruposCatalogo(IEnumerable<ProductoOpcionGrupo>? grupos)
    {
        return GruposEfectivos(grupos)
            .Select(g => (object)new
            {
                id = g.Id,
                nombre = g.Nombre,
                obligatorio = g.Obligatorio,
                minSeleccion = g.MinSeleccion,
                maxSeleccion = g.MaxSeleccion,
                orden = g.Orden,
                opciones = g.Opciones.Where(o => o.Activo).OrderBy(o => o.Orden).Select(o => new
                {
                    id = o.Id,
                    nombre = o.Nombre,
                    precioAdicional = o.PrecioAdicional,
                    orden = o.Orden,
                    activo = o.Activo
                }).ToArray()
            })
            .ToArray();
    }

    public static object[] MapOpcionesLineaRespuesta(IEnumerable<FacturaServicioOpcionSeleccion>? selecciones) =>
        (selecciones ?? Array.Empty<FacturaServicioOpcionSeleccion>())
            .OrderBy(s => s.NombreGrupo)
            .ThenBy(s => s.NombreOpcion)
            .Select(s => (object)new
            {
                grupoId = s.ProductoOpcionGrupoId,
                opcionId = s.ProductoOpcionItemId,
                nombreGrupo = s.NombreGrupo,
                nombreOpcion = s.NombreOpcion,
                precioAdicional = s.PrecioAdicional
            })
            .ToArray();
}
