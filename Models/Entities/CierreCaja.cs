using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Representa un cierre de caja diario
/// </summary>
[Table("CierresCaja")]
public class CierreCaja
{
    public int Id { get; set; }
    
    /// <summary>
    /// Fecha del cierre (solo fecha, sin hora)
    /// </summary>
    public DateTime FechaCierre { get; set; }
    
    /// <summary>
    /// Fecha y hora exacta del cierre
    /// </summary>
    public DateTime FechaHoraCierre { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Usuario que realizó el cierre
    /// </summary>
    public int UsuarioId { get; set; }
    
    /// <summary>
    /// Monto inicial de caja (si hay apertura)
    /// </summary>
    public decimal? MontoInicial { get; set; }
    
    // ========== TOTALES POR MÉTODO DE PAGO ==========
    public decimal TotalEfectivo { get; set; }
    public decimal TotalTarjeta { get; set; }
    public decimal TotalTransferencia { get; set; }
    
    // ========== TOTALES POR MONEDA ==========
    public decimal TotalCordobas { get; set; }
    public decimal TotalDolares { get; set; }
    
    // ========== TOTALES GENERALES ==========
    public decimal TotalGeneral { get; set; }
    public int TotalOrdenes { get; set; }
    public int TotalPagos { get; set; }
    
    // ========== ARQUEO DE CAJA ==========
    /// <summary>
    /// Monto esperado en caja (calculado)
    /// </summary>
    public decimal MontoEsperado { get; set; }
    
    /// <summary>
    /// Monto real contado en caja (físico)
    /// </summary>
    public decimal? MontoReal { get; set; }
    
    /// <summary>
    /// Diferencia entre esperado y real
    /// </summary>
    public decimal? Diferencia { get; set; }
    
    /// <summary>
    /// Observaciones del cierre
    /// </summary>
    public string? Observaciones { get; set; }
    
    /// <summary>
    /// Estado del cierre (Abierto, Cerrado, Revisado)
    /// </summary>
    public string Estado { get; set; } = "Cerrado";
    
    // Relaciones
    public virtual Usuario Usuario { get; set; } = null!;
}

