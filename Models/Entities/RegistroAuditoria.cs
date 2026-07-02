using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

[Table("RegistrosAuditoria")]
public class RegistroAuditoria
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public int? UsuarioId { get; set; }
    
    [ForeignKey("UsuarioId")]
    public virtual Usuario? Usuario { get; set; }
    
    public string? NombreUsuario { get; set; }
    public string? RolUsuario { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string? MesaNumero { get; set; }
    public int? PedidoId { get; set; }
    public string? DetallesJson { get; set; }
}
