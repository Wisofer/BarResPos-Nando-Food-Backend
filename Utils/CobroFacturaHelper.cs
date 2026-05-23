using BarRestPOS.Models.Entities;

namespace BarRestPOS.Utils;

/// <summary>
/// Totales de cobro por pedido: <see cref="Factura.Monto"/> = consumo (subtotal líneas);
/// neto cobrado y descuento viven en <see cref="Pago"/> (y reparto en <see cref="PagoFactura"/>).
/// </summary>
public static class CobroFacturaHelper
{
    public static decimal NetoAplicadoAPedido(Pago pago, int facturaId)
    {
        if (pago.PagoFacturas is { Count: > 0 })
        {
            var pf = pago.PagoFacturas.FirstOrDefault(x => x.FacturaId == facturaId);
            return pf?.MontoAplicado ?? 0m;
        }

        return pago.FacturaId == facturaId ? pago.Monto : 0m;
    }

    public static decimal DescuentoAtribuidoAPedido(Pago pago, int facturaId)
    {
        if (pago.PagoFacturas is { Count: > 0 })
        {
            var pf = pago.PagoFacturas.FirstOrDefault(x => x.FacturaId == facturaId);
            if (pf == null) return 0m;
            if (pago.Monto <= 0) return 0m;
            return Math.Round(pago.DescuentoMonto * (pf.MontoAplicado / pago.Monto), 2, MidpointRounding.AwayFromZero);
        }

        return pago.FacturaId == facturaId ? pago.DescuentoMonto : 0m;
    }

    public static Dictionary<int, decimal> NetoCobradoPorFactura(IEnumerable<int> facturaIds, IEnumerable<Pago> pagos)
    {
        var idSet = facturaIds as HashSet<int> ?? facturaIds.ToHashSet();
        var dict = idSet.ToDictionary(id => id, _ => 0m);

        foreach (var p in pagos)
        {
            if (p.PagoFacturas is { Count: > 0 })
            {
                foreach (var pf in p.PagoFacturas)
                {
                    if (idSet.Contains(pf.FacturaId))
                        dict[pf.FacturaId] += pf.MontoAplicado;
                }
            }
            else if (p.FacturaId.HasValue && idSet.Contains(p.FacturaId.Value))
                dict[p.FacturaId.Value] += p.Monto;
        }

        foreach (var k in dict.Keys.ToList())
            dict[k] = Math.Round(dict[k], 2, MidpointRounding.AwayFromZero);

        return dict;
    }

    public static Dictionary<int, decimal> DescuentoPorFactura(IEnumerable<int> facturaIds, IEnumerable<Pago> pagos)
    {
        var idSet = facturaIds as HashSet<int> ?? facturaIds.ToHashSet();
        var dict = idSet.ToDictionary(id => id, _ => 0m);

        foreach (var p in pagos)
        {
            foreach (var fid in FacturasAfectadasPorPago(p, idSet))
                dict[fid] += DescuentoAtribuidoAPedido(p, fid);
        }

        foreach (var k in dict.Keys.ToList())
            dict[k] = Math.Round(dict[k], 2, MidpointRounding.AwayFromZero);

        return dict;
    }

    /// <summary>Suma del neto cobrado (C$) para el conjunto de pedidos indicado.</summary>
    public static decimal SumNetoCobrado(IEnumerable<int> facturaIds, IEnumerable<Pago> pagos)
    {
        var d = NetoCobradoPorFactura(facturaIds, pagos);
        return Math.Round(d.Values.Sum(), 2, MidpointRounding.AwayFromZero);
    }

    private static IEnumerable<int> FacturasAfectadasPorPago(Pago p, HashSet<int> limitarA)
    {
        if (p.PagoFacturas is { Count: > 0 })
        {
            foreach (var pf in p.PagoFacturas)
            {
                if (limitarA.Contains(pf.FacturaId))
                    yield return pf.FacturaId;
            }
        }
        else if (p.FacturaId.HasValue && limitarA.Contains(p.FacturaId.Value))
            yield return p.FacturaId.Value;
    }
}
