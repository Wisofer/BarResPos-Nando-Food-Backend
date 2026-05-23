using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

[Table("RefreshTokens")]
public class RefreshToken
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string JwtId { get; set; } = string.Empty;
    public DateTime CreadoEnUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiraEnUtc { get; set; }
    public DateTime? RevocadoEnUtc { get; set; }
    public string? ReemplazadoPorTokenHash { get; set; }
    public string? MotivoRevocacion { get; set; }

    [NotMapped]
    public bool Activo => RevocadoEnUtc == null && DateTime.UtcNow < ExpiraEnUtc;

    public virtual Usuario Usuario { get; set; } = null!;
}
