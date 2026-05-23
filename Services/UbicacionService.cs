using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class UbicacionService : IUbicacionService
{
    private readonly ApplicationDbContext _context;

    public UbicacionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<Ubicacion> ObtenerTodas()
    {
        return _context.Ubicaciones
            .OrderBy(u => u.Nombre)
            .ToList();
    }

    public List<Ubicacion> ObtenerActivas()
    {
        return _context.Ubicaciones
            .Where(u => u.Activo)
            .OrderBy(u => u.Nombre)
            .ToList();
    }

    public Ubicacion? ObtenerPorId(int id)
    {
        return _context.Ubicaciones
            .FirstOrDefault(u => u.Id == id);
    }

    public Ubicacion Crear(Ubicacion ubicacion)
    {
        if (ExisteNombre(ubicacion.Nombre))
        {
            throw new Exception($"Ya existe una ubicación con el nombre '{ubicacion.Nombre}'");
        }

        ubicacion.FechaCreacion = DateTime.Now;
        ubicacion.Activo = true;

        _context.Ubicaciones.Add(ubicacion);
        _context.SaveChanges();
        return ubicacion;
    }

    public Ubicacion Actualizar(Ubicacion ubicacion)
    {
        var existente = ObtenerPorId(ubicacion.Id);
        if (existente == null)
            throw new Exception("Ubicación no encontrada");

        if (ExisteNombre(ubicacion.Nombre, ubicacion.Id))
        {
            throw new Exception($"Ya existe otra ubicación con el nombre '{ubicacion.Nombre}'");
        }

        existente.Nombre = ubicacion.Nombre;
        existente.Descripcion = ubicacion.Descripcion;
        existente.Activo = ubicacion.Activo;

        _context.SaveChanges();
        return existente;
    }

    public bool Eliminar(int id)
    {
        var ubicacion = ObtenerPorId(id);
        if (ubicacion == null)
            return false;

        // Verificar si tiene mesas asociadas
        var tieneMesas = _context.Mesas.Any(m => m.UbicacionId == id);
        if (tieneMesas)
            return false; // No se puede eliminar si tiene mesas asociadas

        _context.Ubicaciones.Remove(ubicacion);
        _context.SaveChanges();
        return true;
    }

    public bool ExisteNombre(string nombre, int? idExcluir = null)
    {
        return _context.Ubicaciones
            .Any(u => u.Nombre.ToLower() == nombre.ToLower() && (idExcluir == null || u.Id != idExcluir));
    }
}

