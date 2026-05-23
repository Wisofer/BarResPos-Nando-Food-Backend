namespace BarRestPOS.Models.Api;

/// <summary>Cancelar pedido (mesa, delivery o llevar) con PIN de verificación.</summary>
public class CancelarPedidoRequest
{
    public string? Codigo { get; set; }
}

public class ActualizarPedidoRequest
{
    public int? MesaId { get; set; }
    public int? ClienteId { get; set; }
    public int? MeseroId { get; set; }
    public string? Estado { get; set; }
    public string? EstadoCocina { get; set; }
    public string? Observaciones { get; set; }
    public List<ActualizarPedidoItemRequest>? Items { get; set; }
}

public class ActualizarPedidoItemRequest
{
    public int ServicioId { get; set; }
    public int Cantidad { get; set; }
    public decimal? PrecioUnitario { get; set; }
    public string? Estado { get; set; }
    public string? Notas { get; set; }

    /// <summary>Si el producto tiene grupos de opciones, enviar selección; si no, omitir o vacío.</summary>
    public List<OpcionSeleccionRequest>? OpcionesSeleccionadas { get; set; }
}
