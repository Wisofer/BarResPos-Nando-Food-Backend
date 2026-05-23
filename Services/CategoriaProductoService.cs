using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class CategoriaProductoService : ICategoriaProductoService
{
    private readonly ApplicationDbContext _context;

    public CategoriaProductoService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<CategoriaProducto> ObtenerTodas()
    {
        return _context.CategoriasProducto
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Nombre)
            .ToList();
    }

    public List<CategoriaProducto> ObtenerActivas()
    {
        return _context.CategoriasProducto
            .Where(c => c.Activo)
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Nombre)
            .ToList();
    }

    public CategoriaProducto? ObtenerPorId(int id)
    {
        return _context.CategoriasProducto
            .Include(c => c.Productos)
            .FirstOrDefault(c => c.Id == id);
    }

    public CategoriaProducto? ObtenerPorNombre(string nombre)
    {
        return _context.CategoriasProducto
            .FirstOrDefault(c => c.Nombre == nombre);
    }

    public CategoriaProducto Crear(CategoriaProducto categoria)
    {
        // Validar que el nombre no esté vacío
        if (string.IsNullOrWhiteSpace(categoria.Nombre))
        {
            throw new Exception("El nombre de la categoría es requerido.");
        }

        // Validar que el nombre sea único
        var existe = _context.CategoriasProducto.Any(c => c.Nombre == categoria.Nombre);
        if (existe)
        {
            throw new Exception($"Ya existe una categoría con el nombre '{categoria.Nombre}'.");
        }

        categoria.FechaCreacion = DateTime.Now;
        categoria.Activo = true;

        // Si no se especifica orden, asignar el siguiente número
        if (categoria.Orden == 0)
        {
            var maxOrden = _context.CategoriasProducto.Max(c => (int?)c.Orden) ?? 0;
            categoria.Orden = maxOrden + 1;
        }

        _context.CategoriasProducto.Add(categoria);
        _context.SaveChanges();
        return categoria;
    }

    public CategoriaProducto Actualizar(CategoriaProducto categoria)
    {
        var existente = _context.CategoriasProducto.FirstOrDefault(c => c.Id == categoria.Id);
        if (existente == null)
        {
            throw new Exception("Categoría no encontrada.");
        }

        // Validar que el nombre no esté vacío
        if (string.IsNullOrWhiteSpace(categoria.Nombre))
        {
            throw new Exception("El nombre de la categoría es requerido.");
        }

        // Validar que el nombre sea único (excepto la misma categoría)
        var existeConMismoNombre = _context.CategoriasProducto
            .Any(c => c.Nombre == categoria.Nombre && c.Id != categoria.Id);
        if (existeConMismoNombre)
        {
            throw new Exception($"Ya existe otra categoría con el nombre '{categoria.Nombre}'.");
        }

        existente.Nombre = categoria.Nombre;
        existente.Descripcion = categoria.Descripcion;
        existente.IconoNombre = categoria.IconoNombre;
        existente.Orden = categoria.Orden;
        existente.RequiereCocina = categoria.RequiereCocina;
        existente.Activo = categoria.Activo;

        _context.SaveChanges();
        return existente;
    }

    public bool Eliminar(int id)
    {
        var categoria = _context.CategoriasProducto
            .Include(c => c.Productos)
            .FirstOrDefault(c => c.Id == id);
        
        if (categoria == null)
        {
            return false;
        }

        // Verificar si tiene productos asociados
        if (categoria.Productos.Any())
        {
            throw new Exception($"No se puede eliminar la categoría '{categoria.Nombre}' porque tiene productos asociados. Desactívala en lugar de eliminarla.");
        }

        _context.CategoriasProducto.Remove(categoria);
        _context.SaveChanges();
        return true;
    }
}

