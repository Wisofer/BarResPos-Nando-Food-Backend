using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface ICategoriaProductoService
{
    List<CategoriaProducto> ObtenerTodas();
    List<CategoriaProducto> ObtenerActivas();
    CategoriaProducto? ObtenerPorId(int id);
    CategoriaProducto? ObtenerPorNombre(string nombre);
    CategoriaProducto Crear(CategoriaProducto categoria);
    CategoriaProducto Actualizar(CategoriaProducto categoria);
    bool Eliminar(int id);
}

