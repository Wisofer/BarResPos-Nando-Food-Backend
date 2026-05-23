using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class ServicioService : IServicioService
{
    private readonly ApplicationDbContext _context;

    public ServicioService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<Servicio> ObtenerTodos()
    {
        return _context.Servicios.ToList();
    }

    public List<Servicio> ObtenerActivos()
    {
        return _context.Servicios.Where(s => s.Activo).ToList();
    }

    public List<Servicio> ObtenerPorCategoria(string categoria)
    {
        return _context.Servicios.Where(s => s.Categoria == categoria).ToList();
    }

    public List<Servicio> ObtenerActivosPorCategoria(string categoria)
    {
        return _context.Servicios.Where(s => s.Activo && s.Categoria == categoria).ToList();
    }

    public Servicio? ObtenerPorId(int id)
    {
        return _context.Servicios.FirstOrDefault(s => s.Id == id);
    }

    public Servicio Crear(Servicio servicio)
    {
        servicio.FechaCreacion = DateTime.Now;
        servicio.Activo = true;
        _context.Servicios.Add(servicio);
        _context.SaveChanges();
        return servicio;
    }

    public Servicio Actualizar(Servicio servicio)
    {
        var existente = ObtenerPorId(servicio.Id);
        if (existente == null)
            throw new Exception("Servicio no encontrado");

        existente.Nombre = servicio.Nombre;
        existente.Descripcion = servicio.Descripcion;
        existente.Precio = servicio.Precio;
        existente.Categoria = servicio.Categoria;
        existente.Activo = servicio.Activo;
        existente.ControlarStock = servicio.ControlarStock;
        existente.StockMinimo = servicio.StockMinimo;
        // Nota: Stock no se actualiza aquí, se actualiza desde el módulo de Inventario

        _context.SaveChanges();
        return existente;
    }

    public bool Eliminar(int id)
    {
        var servicio = ObtenerPorId(id);
        if (servicio == null)
            return false;

        // Verificar si tiene órdenes asociadas (a través de FacturaServicio)
        var tieneOrdenes = _context.FacturaServicios.Any(fs => fs.ServicioId == id);
        if (tieneOrdenes)
        {
            throw new Exception("No se puede eliminar el producto porque tiene órdenes asociadas. Desactívalo en lugar de eliminarlo.");
        }

        // Eliminar físicamente el producto
        _context.Servicios.Remove(servicio);
        _context.SaveChanges();
        return true;
    }
}

