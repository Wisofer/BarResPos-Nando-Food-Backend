using BarRestPOS.Models.Entities;

namespace BarRestPOS.Utils;

/// <summary>
/// Totales para arqueo / preview de cierre. Alineado con <see cref="Pago.Monto"/> (neto cobrado en C$).
/// En efectivo, MontoCordobasFisico guarda lo entregado (puede exceder el neto si hay vuelto); el neto retenido es Pago.Monto.
/// </summary>
public static class CajaArqueoHelper
{
    /// <summary>Efectivo neto retenido (C$) por cobros en efectivo y parte física de mixto.</summary>
    public static decimal TotalEfectivoNetoArqueo(IEnumerable<Pago> pagos, decimal tipoCambioDefecto)
    {
        return pagos.Sum(p =>
        {
            if (p.TipoPago == "Efectivo")
                return p.Monto;
            if (p.TipoPago == "Mixto")
                return (p.MontoCordobasFisico ?? 0) + ((p.MontoDolaresFisico ?? 0) * (p.TipoCambio ?? tipoCambioDefecto));
            return 0m;
        });
    }
}
