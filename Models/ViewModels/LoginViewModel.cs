using System.ComponentModel.DataAnnotations;

namespace BarRestPOS.Models.ViewModels;

public class LoginViewModel
{
    [Display(Name = "Nombre de Usuario")]
    public string? NombreUsuario { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string? Contrasena { get; set; }

    [Display(Name = "Recordarme")]
    public bool Recordarme { get; set; }
}

