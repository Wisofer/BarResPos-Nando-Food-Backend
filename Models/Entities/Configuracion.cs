using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Entidad para almacenar configuraciones del sistema
/// </summary>
[Table("Configuraciones")]
public class Configuracion
{
    public int Id { get; set; }
    public string Clave { get; set; } = string.Empty; // Ej: "TipoCambioDolar"
    public string Valor { get; set; } = string.Empty; // Ej: "36.80"
    public string? Descripcion { get; set; } // Ej: "Tipo de cambio dólar a córdoba"
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? FechaActualizacion { get; set; }
    public string? UsuarioActualizacion { get; set; } // Usuario que hizo el último cambio
}

