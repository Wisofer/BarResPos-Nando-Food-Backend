using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface IAuthService
{
    Usuario? ValidarUsuario(string nombreUsuario, string contrasena);
    bool EsAdministrador(Usuario usuario);
    bool EsUsuarioNormal(Usuario usuario);
}

