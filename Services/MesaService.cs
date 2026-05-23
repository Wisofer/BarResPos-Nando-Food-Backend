using Microsoft.EntityFrameworkCore;
using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;

namespace BarRestPOS.Services;

public class MesaService : IMesaService
{
    private readonly ApplicationDbContext _context;

    public MesaService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<Mesa> ObtenerTodas()
    {
        return _context.Mesas
            .Include(m => m.Ubicacion)
            .Include(m => m.Ordenes)
            .Where(m => m.Activo)
            .OrderBy(m => m.Numero)
            .ToList();
    }

    public List<Mesa> ObtenerPorUbicacion(int ubicacionId)
    {
        return _context.Mesas
            .Include(m => m.Ubicacion)
            .Include(m => m.Ordenes)
            .Where(m => m.UbicacionId == ubicacionId && m.Activo)
            .OrderBy(m => m.Numero)
            .ToList();
    }

    public List<Mesa> ObtenerPorEstado(string estado)
    {
        return _context.Mesas
            .Include(m => m.Ubicacion)
            .Include(m => m.Ordenes)
            .Where(m => m.Estado == estado && m.Activo)
            .OrderBy(m => m.Numero)
            .ToList();
    }

    public Mesa? ObtenerPorId(int id)
    {
        return _context.Mesas
            .Include(m => m.Ubicacion)
            .Include(m => m.Ordenes)
            .FirstOrDefault(m => m.Id == id);
    }

    public Mesa? ObtenerPorNumero(string numero)
    {
        return _context.Mesas
            .Include(m => m.Ubicacion)
            .Include(m => m.Ordenes)
            .FirstOrDefault(m => m.Numero == numero);
    }

    public Mesa Crear(Mesa mesa)
    {
        Console.WriteLine($"[MesaService] Crear - Numero: '{mesa?.Numero}', Capacidad: {mesa?.Capacidad}, UbicacionId: {mesa?.UbicacionId}, Estado: '{mesa?.Estado}'");
        
        // Validar que el número no esté vacío
        if (string.IsNullOrWhiteSpace(mesa?.Numero))
        {
            Console.WriteLine("[MesaService] ERROR: El número de mesa está vacío");
            throw new Exception("El número de mesa es requerido.");
        }
        
        // Validar capacidad
        if (mesa.Capacidad <= 0)
        {
            Console.WriteLine($"[MesaService] ERROR: Capacidad inválida: {mesa.Capacidad}");
            throw new Exception("La capacidad debe ser mayor a cero.");
        }
        
        // Validar que el número sea único (incluyendo mesas desactivadas porque hay índice único en BD)
        var existeMesa = _context.Mesas.Any(m => m.Numero == mesa.Numero);
        Console.WriteLine($"[MesaService] Validación número único - Existe mesa con número '{mesa.Numero}': {existeMesa}");
        
        if (existeMesa)
        {
            var mesaExistente = _context.Mesas.FirstOrDefault(m => m.Numero == mesa.Numero);
            if (mesaExistente != null && mesaExistente.Activo)
            {
                throw new Exception($"Ya existe una mesa activa con el número '{mesa.Numero}'");
            }
            else if (mesaExistente != null && !mesaExistente.Activo)
            {
                // Si la mesa está desactivada, eliminarla físicamente para permitir reutilizar el número
                Console.WriteLine($"[MesaService] Mesa desactivada encontrada con número '{mesa.Numero}', eliminándola físicamente...");
                _context.Mesas.Remove(mesaExistente);
                _context.SaveChanges();
                Console.WriteLine($"[MesaService] Mesa desactivada eliminada, continuando con la creación...");
            }
        }

        // Validar que UbicacionId existe si se proporciona
        if (mesa.UbicacionId.HasValue)
        {
            var ubicacionExiste = _context.Ubicaciones.Any(u => u.Id == mesa.UbicacionId.Value && u.Activo);
            if (!ubicacionExiste)
            {
                throw new Exception($"La ubicación seleccionada no existe o está desactivada.");
            }
        }

        mesa.Estado = SD.EstadoMesaLibre;
        mesa.FechaCreacion = DateTime.Now;
        mesa.Activo = true;

        Console.WriteLine($"[MesaService] Agregando mesa a contexto...");
        Console.WriteLine($"[MesaService] Mesa antes de agregar - Numero: {mesa.Numero}, Capacidad: {mesa.Capacidad}, UbicacionId: {mesa.UbicacionId}, Estado: {mesa.Estado}, Activo: {mesa.Activo}");
        
        try
        {
            _context.Mesas.Add(mesa);
            Console.WriteLine($"[MesaService] Guardando cambios...");
            _context.SaveChanges();
            Console.WriteLine($"[MesaService] Mesa creada exitosamente con ID: {mesa.Id}");
            return mesa;
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine($"[MesaService] ERROR DbUpdateException: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[MesaService] Inner Exception: {ex.InnerException.Message}");
                Console.WriteLine($"[MesaService] Stack Trace: {ex.InnerException.StackTrace}");
                
                // Si es un error de violación de índice único
                if (ex.InnerException.Message.Contains("duplicate key") || 
                    ex.InnerException.Message.Contains("UNIQUE constraint") ||
                    ex.InnerException.Message.Contains("violates unique constraint"))
                {
                    throw new Exception($"Ya existe una mesa con el número '{mesa.Numero}'. Por favor, use un número diferente.");
                }
            }
            throw new Exception($"Error al guardar la mesa: {ex.Message}. Detalles: {ex.InnerException?.Message ?? "Sin detalles adicionales"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MesaService] ERROR General: {ex.Message}");
            Console.WriteLine($"[MesaService] Stack Trace: {ex.StackTrace}");
            throw;
        }
    }

    public Mesa Actualizar(Mesa mesa)
    {
        var existente = _context.Mesas.FirstOrDefault(m => m.Id == mesa.Id);
        if (existente == null)
            throw new Exception("Mesa no encontrada");

        // Validar que el número sea único (excepto para la mesa actual, solo entre mesas activas)
        if (_context.Mesas.Any(m => m.Numero == mesa.Numero && m.Id != mesa.Id && m.Activo))
            throw new Exception($"Ya existe otra mesa activa con el número '{mesa.Numero}'");

        existente.Numero = mesa.Numero;
        existente.Capacidad = mesa.Capacidad;
        existente.UbicacionId = mesa.UbicacionId;
        existente.Estado = mesa.Estado; // Actualizar estado también
        existente.Activo = mesa.Activo;

        _context.SaveChanges();
        return existente;
    }

    public bool CambiarEstado(int id, string nuevoEstado)
    {
        var mesa = _context.Mesas.FirstOrDefault(m => m.Id == id);
        if (mesa == null)
            return false;

        // Validar que el nuevo estado sea válido
        var estadosValidos = new[] { SD.EstadoMesaLibre, SD.EstadoMesaOcupada, SD.EstadoMesaReservada };
        if (!estadosValidos.Contains(nuevoEstado))
            throw new Exception($"Estado '{nuevoEstado}' no es válido");

        mesa.Estado = nuevoEstado;
        _context.SaveChanges();
        return true;
    }

    public bool Eliminar(int id)
    {
        var mesa = _context.Mesas
            .Include(m => m.Ordenes)
            .FirstOrDefault(m => m.Id == id);
        
        if (mesa == null)
            return false;

        // Verificar si tiene órdenes activas
        if (TieneOrdenesActivas(id))
            return false;

        // Soft delete: marcar como inactiva
        mesa.Activo = false;
        _context.SaveChanges();
        return true;
    }

    public bool TieneOrdenesActivas(int id)
    {
        return _context.Facturas.Any(f => 
            f.MesaId == id && 
            f.Estado != SD.EstadoOrdenPagado && 
            f.Estado != SD.EstadoOrdenCancelado);
    }

    public Mesa? ObtenerMesaConOrdenActiva(int mesaId)
    {
        return _context.Mesas
            .Include(m => m.Ubicacion)
            .Include(m => m.Ordenes.Where(o => 
                o.Estado != SD.EstadoOrdenPagado && 
                o.Estado != SD.EstadoOrdenCancelado))
            .ThenInclude(o => o.FacturaServicios)
            .ThenInclude(fs => fs.Servicio)
            .FirstOrDefault(m => m.Id == mesaId);
    }
}

