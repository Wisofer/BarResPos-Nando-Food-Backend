using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface IServicioService
{
    List<Servicio> ObtenerTodos();
    List<Servicio> ObtenerActivos();
    List<Servicio> ObtenerPorCategoria(string categoria);
    List<Servicio> ObtenerActivosPorCategoria(string categoria);
    Servicio? ObtenerPorId(int id);
    Servicio Crear(Servicio servicio);
    Servicio Actualizar(Servicio servicio);
    bool Eliminar(int id);
}

