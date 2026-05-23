namespace BarRestPOS.Utils;

public static class PedidoOrigenHelper
{
    /// <summary>Valor para API: mesa | delivery | llevar</summary>
    public static string TipoDesdeOrigen(string? origenPedido)
    {
        if (string.IsNullOrWhiteSpace(origenPedido))
            return "mesa";
        if (origenPedido == SD.OrigenPedidoDelivery)
            return "delivery";
        if (origenPedido == SD.OrigenPedidoLlevar)
            return "llevar";
        return "mesa";
    }
}
