using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface IUbicacionService
{
    List<Ubicacion> ObtenerTodas();
    List<Ubicacion> ObtenerActivas();
    Ubicacion? ObtenerPorId(int id);
    Ubicacion Crear(Ubicacion ubicacion);
    Ubicacion Actualizar(Ubicacion ubicacion);
    bool Eliminar(int id);
    bool ExisteNombre(string nombre, int? idExcluir = null);
}

