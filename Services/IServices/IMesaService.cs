using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface IMesaService
{
    List<Mesa> ObtenerTodas();
    List<Mesa> ObtenerPorUbicacion(int ubicacionId);
    List<Mesa> ObtenerPorEstado(string estado);
    Mesa? ObtenerPorId(int id);
    Mesa? ObtenerPorNumero(string numero);
    Mesa Crear(Mesa mesa);
    Mesa Actualizar(Mesa mesa);
    bool CambiarEstado(int id, string nuevoEstado);
    bool Eliminar(int id);
    bool TieneOrdenesActivas(int id);
    Mesa? ObtenerMesaConOrdenActiva(int mesaId);
}

