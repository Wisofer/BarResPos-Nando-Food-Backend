using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Producto del restaurante/bar (bebidas, comidas, licores, etc.)
/// </summary>
[Table("Productos")]
public class Servicio
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty; // "BEB001", "COM001", "LIC001"
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    // Precio de venta (se mantiene nombre por compatibilidad histórica)
    public decimal Precio { get; set; }
    // Precio de compra base/referencial del producto
    public decimal PrecioCompra { get; set; }
    public string Categoria { get; set; } = "Bebidas"; // Bebidas, Comidas, Licores, Postres, Promos
    public int? CategoriaProductoId { get; set; } // Relación con CategoriaProducto
    public int Stock { get; set; } = 0; // Cantidad disponible (0 = ilimitado/no controlar)
    public int StockMinimo { get; set; } = 0; // Alerta de stock bajo
    public bool ControlarStock { get; set; } = false; // Si se debe controlar el stock

    public string? ImagenUrl { get; set; } // URL de la imagen del producto
    public int? ProveedorId { get; set; }
    public bool Destacado { get; set; } = false; // Para mostrar en la pantalla principal
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
    
    // Relaciones
    public virtual CategoriaProducto? CategoriaProducto { get; set; }
    public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();
    public virtual ICollection<ClienteServicio> ClienteServicios { get; set; } = new List<ClienteServicio>(); // Relación muchos-a-muchos
    public virtual ICollection<ProductoOpcionGrupo> OpcionGrupos { get; set; } = new List<ProductoOpcionGrupo>();
}

