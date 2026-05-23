namespace BarRestPOS.Services.IServices;

public interface IConfiguracionService
{
    /// <summary>
    /// Obtiene el valor de una configuración por su clave
    /// </summary>
    string? ObtenerValor(string clave);
    
    /// <summary>
    /// Obtiene el valor de una configuración como decimal
    /// </summary>
    decimal? ObtenerValorDecimal(string clave);
    
    /// <summary>
    /// Obtiene todas las configuraciones
    /// </summary>
    List<Models.Entities.Configuracion> ObtenerTodas();
    
    /// <summary>
    /// Obtiene una configuración por su clave
    /// </summary>
    Models.Entities.Configuracion? ObtenerPorClave(string clave);
    
    /// <summary>
    /// Actualiza el valor de una configuración
    /// </summary>
    void ActualizarValor(string clave, string valor, string? usuarioActualizacion = null);
    
    /// <summary>
    /// Crea una nueva configuración si no existe
    /// </summary>
    void CrearSiNoExiste(string clave, string valor, string? descripcion = null);
}

