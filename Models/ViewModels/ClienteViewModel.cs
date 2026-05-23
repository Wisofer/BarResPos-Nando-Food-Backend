namespace BarRestPOS.Models.ViewModels;

public class ClienteViewModel
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Cedula { get; set; }
    public string? Email { get; set; }
    public bool Activo { get; set; }
}

