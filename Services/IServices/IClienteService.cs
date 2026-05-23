using BarRestPOS.Models.Entities;
using BarRestPOS.Models.ViewModels;

namespace BarRestPOS.Services.IServices;

public interface IClienteService
{
    List<Cliente> ObtenerTodos();
    PagedResult<Cliente> ObtenerPaginados(int pagina = 1, int tamanoPagina = 10, string? busqueda = null);
    Cliente? ObtenerPorId(int id);
    Cliente? ObtenerPorCodigo(string codigo);
    List<Cliente> Buscar(string termino);
    Cliente Crear(Cliente cliente);
    Cliente Actualizar(Cliente cliente);
    bool Eliminar(int id);
    bool ExisteCodigo(string codigo, int? idExcluir = null);
    bool ExisteCedula(string? cedula, int? idExcluir = null);
    bool ExisteEmail(string? email, int? idExcluir = null);
    bool ExisteNombreYCedula(string nombre, string? cedula, int? idExcluir = null);
    int ObtenerTotal();
    int ObtenerTotalActivos();
    int ObtenerNuevosEsteMes();
    int ActualizarCodigosExistentes();
    
    // Métodos para gestionar múltiples servicios
    List<ClienteServicio> ObtenerServiciosActivos(int clienteId);
    List<ClienteServicio> ObtenerServicios(int clienteId);
    void AsignarServicios(int clienteId, List<int> servicioIds);
    void AsignarServiciosConCantidad(int clienteId, Dictionary<int, int> serviciosConCantidad);
    void ActivarServicio(int clienteId, int servicioId);
    void DesactivarServicio(int clienteId, int servicioId);
    bool TieneServicioActivo(int clienteId, int servicioId);
}

