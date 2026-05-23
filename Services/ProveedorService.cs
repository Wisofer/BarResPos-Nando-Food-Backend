using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class ProveedorService : IProveedorService
{
    private readonly ApplicationDbContext _context;

    public ProveedorService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<Proveedor> ObtenerTodos()
    {
        return _context.Proveedores
            .OrderBy(p => p.Nombre)
            .ToList();
    }

    public List<Proveedor> ObtenerActivos()
    {
        return _context.Proveedores
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .ToList();
    }

    public Proveedor? ObtenerPorId(int id)
    {
        return _context.Proveedores.FirstOrDefault(p => p.Id == id);
    }

    public Proveedor Crear(Proveedor proveedor)
    {
        proveedor.FechaCreacion = DateTime.Now;
        proveedor.Activo = true;
        _context.Proveedores.Add(proveedor);
        _context.SaveChanges();
        return proveedor;
    }

    public Proveedor Actualizar(Proveedor proveedor)
    {
        var existente = ObtenerPorId(proveedor.Id);
        if (existente == null)
            throw new Exception("Proveedor no encontrado");

        existente.Nombre = proveedor.Nombre;
        existente.Telefono = proveedor.Telefono;
        existente.Email = proveedor.Email;
        existente.Direccion = proveedor.Direccion;
        existente.Contacto = proveedor.Contacto;
        existente.Observaciones = proveedor.Observaciones;
        existente.Activo = proveedor.Activo;

        _context.SaveChanges();
        return existente;
    }

    public bool Eliminar(int id)
    {
        var proveedor = ObtenerPorId(id);
        if (proveedor == null)
            return false;

        // Verificar si tiene movimientos de inventario asociados
        var tieneMovimientos = _context.MovimientosInventario.Any(m => m.ProveedorId == id);
        if (tieneMovimientos)
        {
            throw new Exception("No se puede eliminar el proveedor porque tiene movimientos de inventario asociados. Desactívalo en lugar de eliminarlo.");
        }

        _context.Proveedores.Remove(proveedor);
        _context.SaveChanges();
        return true;
    }
}

