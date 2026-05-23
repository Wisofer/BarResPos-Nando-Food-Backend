namespace BarRestPOS.Services;

public interface IPdfService
{
    byte[] GenerarPdfFactura(Models.Entities.Factura factura);
    byte[] GenerarPdfPedido(Models.Entities.Factura pedido);
}

