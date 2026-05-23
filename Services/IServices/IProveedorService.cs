using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface IProveedorService
{
    List<Proveedor> ObtenerTodos();
    List<Proveedor> ObtenerActivos();
    Proveedor? ObtenerPorId(int id);
    Proveedor Crear(Proveedor proveedor);
    Proveedor Actualizar(Proveedor proveedor);
    bool Eliminar(int id);
}

