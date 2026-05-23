using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Models.ViewModels;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class ClienteService : IClienteService
{
    private readonly ApplicationDbContext _context;

    public ClienteService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<Cliente> ObtenerTodos()
    {
        return _context.Clientes
            .OrderByDescending(c => c.FechaCreacion)
            .ToList();
    }

    public PagedResult<Cliente> ObtenerPaginados(int pagina = 1, int tamanoPagina = 10, string? busqueda = null)
    {
        var query = _context.Clientes.AsQueryable();

        // Aplicar búsqueda si existe
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var termino = busqueda.ToLower();
            
            // Intentar parsear como número para buscar en TotalFacturas
            bool esNumero = int.TryParse(busqueda.Trim(), out int numeroFacturas);
            
            query = query.Where(c => 
                c.Nombre.ToLower().Contains(termino) ||
                c.Codigo.ToLower().Contains(termino) ||
                (c.Cedula != null && c.Cedula.ToLower().Contains(termino)) ||
                (c.Telefono != null && c.Telefono.Contains(termino)) ||
                (esNumero && c.TotalFacturas == numeroFacturas));
        }

        var totalItems = query.Count();

        var items = query
            .Include(c => c.ClienteServicios)
                .ThenInclude(cs => cs.Servicio)
            .OrderByDescending(c => c.FechaCreacion)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToList();

        return new PagedResult<Cliente>
        {
            Items = items,
            CurrentPage = pagina,
            PageSize = tamanoPagina,
            TotalItems = totalItems
        };
    }

    public int ObtenerTotal()
    {
        return _context.Clientes.Count();
    }

    public int ObtenerTotalActivos()
    {
        return _context.Clientes.Count(c => c.Activo);
    }

    public int ObtenerNuevosEsteMes()
    {
        var ahora = DateTime.Now;
        return _context.Clientes
            .Count(c => c.FechaCreacion.Month == ahora.Month && c.FechaCreacion.Year == ahora.Year);
    }

    public Cliente? ObtenerPorId(int id)
    {
        return _context.Clientes
            .Include(c => c.Servicio)
            .Include(c => c.ClienteServicios)
                .ThenInclude(cs => cs.Servicio)
            .FirstOrDefault(c => c.Id == id);
    }

    public Cliente? ObtenerPorCodigo(string codigo)
    {
        return _context.Clientes.FirstOrDefault(c => c.Codigo == codigo);
    }

    public List<Cliente> Buscar(string termino)
    {
        if (string.IsNullOrWhiteSpace(termino))
            return ObtenerTodos();

        termino = termino.ToLower();
        return _context.Clientes
            .Where(c => c.Nombre.ToLower().Contains(termino) ||
                       c.Codigo.ToLower().Contains(termino) ||
                       (c.Cedula != null && c.Cedula.ToLower().Contains(termino)) ||
                       (c.Telefono != null && c.Telefono.Contains(termino)))
            .OrderByDescending(c => c.FechaCreacion)
            .ToList();
    }

    public Cliente Crear(Cliente cliente)
    {
        // Validar duplicados antes de crear
        if (ExisteNombreYCedula(cliente.Nombre, cliente.Cedula))
        {
            throw new Exception($"Ya existe un cliente con el nombre '{cliente.Nombre}'" + 
                              (string.IsNullOrWhiteSpace(cliente.Cedula) ? "" : $" y cédula '{cliente.Cedula}'"));
        }

        if (!string.IsNullOrWhiteSpace(cliente.Cedula) && ExisteCedula(cliente.Cedula))
        {
            throw new Exception($"Ya existe un cliente con la cédula '{cliente.Cedula}'");
        }

        if (!string.IsNullOrWhiteSpace(cliente.Email) && ExisteEmail(cliente.Email))
        {
            throw new Exception($"Ya existe un cliente con el email '{cliente.Email}'");
        }

        cliente.FechaCreacion = DateTime.Now;
        cliente.Activo = true;
        
        // Generar código automáticamente si no viene
        if (string.IsNullOrWhiteSpace(cliente.Codigo))
        {
            cliente.Codigo = CodigoHelper.GenerarCodigoClienteUnico(codigo => ExisteCodigo(codigo));
        }
        
        _context.Clientes.Add(cliente);
        _context.SaveChanges();
        return cliente;
    }

    public Cliente Actualizar(Cliente cliente)
    {
        var existente = ObtenerPorId(cliente.Id);
        if (existente == null)
            throw new Exception("Cliente no encontrado");

        // Validar duplicados antes de actualizar (excluyendo el cliente actual)
        if (ExisteNombreYCedula(cliente.Nombre, cliente.Cedula, cliente.Id))
        {
            throw new Exception($"Ya existe otro cliente con el nombre '{cliente.Nombre}'" + 
                              (string.IsNullOrWhiteSpace(cliente.Cedula) ? "" : $" y cédula '{cliente.Cedula}'"));
        }

        if (!string.IsNullOrWhiteSpace(cliente.Cedula) && ExisteCedula(cliente.Cedula, cliente.Id))
        {
            throw new Exception($"Ya existe otro cliente con la cédula '{cliente.Cedula}'");
        }

        if (!string.IsNullOrWhiteSpace(cliente.Email) && ExisteEmail(cliente.Email, cliente.Id))
        {
            throw new Exception($"Ya existe otro cliente con el email '{cliente.Email}'");
        }

        existente.Codigo = cliente.Codigo;
        existente.Nombre = cliente.Nombre;
        existente.Telefono = cliente.Telefono;
        existente.Cedula = cliente.Cedula;
        existente.Email = cliente.Email;
        existente.Activo = cliente.Activo;
        existente.FechaCreacion = cliente.FechaCreacion;
        existente.TotalFacturas = cliente.TotalFacturas; // Permitir edición manual
        existente.ServicioId = cliente.ServicioId; // Mantener para compatibilidad

        _context.SaveChanges();
        return existente;
    }

    public bool Eliminar(int id)
    {
        try
        {
            var cliente = ObtenerPorId(id);
            if (cliente == null)
                return false;

            // Verificar si tiene facturas asociadas
            var tieneFacturas = _context.Facturas.Any(f => f.ClienteId == id);
            if (tieneFacturas)
                return false; // No se puede eliminar si tiene facturas

            _context.Clientes.Remove(cliente);
            _context.SaveChanges();
            return true;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Error de restricción de clave foránea - el cliente tiene facturas o pagos asociados
            return false;
        }
        catch (Exception)
        {
            // Otro tipo de error
            return false;
        }
    }

    public bool ExisteCodigo(string codigo, int? idExcluir = null)
    {
        return _context.Clientes
            .Any(c => c.Codigo == codigo && (idExcluir == null || c.Id != idExcluir));
    }

    public bool ExisteCedula(string? cedula, int? idExcluir = null)
    {
        if (string.IsNullOrWhiteSpace(cedula))
            return false;
        
        return _context.Clientes
            .Any(c => c.Cedula != null && 
                     c.Cedula.Trim().ToUpper() == cedula.Trim().ToUpper() && 
                     (idExcluir == null || c.Id != idExcluir));
    }

    public bool ExisteEmail(string? email, int? idExcluir = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        
        return _context.Clientes
            .Any(c => c.Email != null && 
                     c.Email.Trim().ToLower() == email.Trim().ToLower() && 
                     (idExcluir == null || c.Id != idExcluir));
    }

    public bool ExisteNombreYCedula(string nombre, string? cedula, int? idExcluir = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return false;
        
        var nombreNormalizado = nombre.Trim().ToUpper();
        var query = _context.Clientes
            .Where(c => c.Nombre.Trim().ToUpper() == nombreNormalizado);
        
        if (!string.IsNullOrWhiteSpace(cedula))
        {
            var cedulaNormalizada = cedula.Trim().ToUpper();
            query = query.Where(c => c.Cedula != null && c.Cedula.Trim().ToUpper() == cedulaNormalizada);
        }
        
        if (idExcluir.HasValue)
        {
            query = query.Where(c => c.Id != idExcluir.Value);
        }
        
        return query.Any();
    }

    public int ActualizarCodigosExistentes()
    {
        // Optimización: procesar en lotes para evitar cargar todos los clientes en memoria
        int actualizados = 0;
        const int batchSize = 100;
        int skip = 0;
        
        while (true)
        {
            var clientes = _context.Clientes
                .Where(c => !c.Codigo.StartsWith("EMS_"))
                .Skip(skip)
                .Take(batchSize)
                .ToList();

            if (!clientes.Any())
                break;

            foreach (var cliente in clientes)
            {
                var nuevoCodigo = CodigoHelper.GenerarCodigoClienteUnico(codigo => 
                    _context.Clientes.Any(c => c.Codigo == codigo && c.Id != cliente.Id));
                
                cliente.Codigo = nuevoCodigo;
                actualizados++;
            }

            _context.SaveChanges();
            skip += batchSize;
        }

        return actualizados;
    }

    // Métodos para gestionar múltiples servicios
    public List<ClienteServicio> ObtenerServiciosActivos(int clienteId)
    {
        return _context.ClienteServicios
            .Include(cs => cs.Servicio)
            .Where(cs => cs.ClienteId == clienteId && cs.Activo)
            .OrderBy(cs => cs.FechaInicio)
            .ToList();
    }

    public List<ClienteServicio> ObtenerServicios(int clienteId)
    {
        return _context.ClienteServicios
            .Include(cs => cs.Servicio)
            .Where(cs => cs.ClienteId == clienteId)
            .OrderByDescending(cs => cs.FechaInicio)
            .ToList();
    }

    public void AsignarServicios(int clienteId, List<int> servicioIds)
    {
        if (servicioIds == null || !servicioIds.Any())
            return;

        var cliente = ObtenerPorId(clienteId);
        if (cliente == null)
            throw new Exception("Cliente no encontrado");

        // Obtener servicios actuales activos del cliente
        var serviciosActuales = _context.ClienteServicios
            .Where(cs => cs.ClienteId == clienteId && cs.Activo)
            .ToList();

        // Desactivar servicios que ya no están en la lista
        var serviciosADesactivar = serviciosActuales
            .Where(cs => !servicioIds.Contains(cs.ServicioId))
            .ToList();

        foreach (var clienteServicio in serviciosADesactivar)
        {
            clienteServicio.Activo = false;
            clienteServicio.FechaFin = DateTime.Now;
        }

        // Activar o crear servicios nuevos
        foreach (var servicioId in servicioIds)
        {
            var clienteServicioExistente = serviciosActuales
                .FirstOrDefault(cs => cs.ServicioId == servicioId);

            if (clienteServicioExistente != null)
            {
                // Si existe pero está desactivado, reactivarlo
                if (!clienteServicioExistente.Activo)
                {
                    clienteServicioExistente.Activo = true;
                    clienteServicioExistente.FechaInicio = DateTime.Now;
                    clienteServicioExistente.FechaFin = null;
                }
            }
            else
            {
                // Crear nuevo ClienteServicio
                var nuevoClienteServicio = new ClienteServicio
                {
                    ClienteId = clienteId,
                    ServicioId = servicioId,
                    Cantidad = 1, // Por defecto 1
                    Activo = true,
                    FechaInicio = DateTime.Now,
                    FechaCreacion = DateTime.Now
                };
                _context.ClienteServicios.Add(nuevoClienteServicio);
            }
        }

        // Actualizar ServicioId del cliente con el primer servicio activo (para compatibilidad)
        var primerServicioActivo = _context.ClienteServicios
            .Where(cs => cs.ClienteId == clienteId && cs.Activo)
            .OrderBy(cs => cs.FechaInicio)
            .FirstOrDefault();

        if (primerServicioActivo != null)
        {
            cliente.ServicioId = primerServicioActivo.ServicioId;
        }
        else
        {
            cliente.ServicioId = null;
        }

        _context.SaveChanges();
    }

    public void AsignarServiciosConCantidad(int clienteId, Dictionary<int, int> serviciosConCantidad)
    {
        if (serviciosConCantidad == null || !serviciosConCantidad.Any())
            return;

        var cliente = ObtenerPorId(clienteId);
        if (cliente == null)
            throw new Exception("Cliente no encontrado");

        // Obtener servicios actuales activos del cliente
        var serviciosActuales = _context.ClienteServicios
            .Where(cs => cs.ClienteId == clienteId && cs.Activo)
            .ToList();

        var servicioIds = serviciosConCantidad.Keys.ToList();

        // Desactivar servicios que ya no están en la lista
        var serviciosADesactivar = serviciosActuales
            .Where(cs => !servicioIds.Contains(cs.ServicioId))
            .ToList();

        foreach (var clienteServicio in serviciosADesactivar)
        {
            clienteServicio.Activo = false;
            clienteServicio.FechaFin = DateTime.Now;
        }

        // Activar o crear servicios nuevos con cantidad
        foreach (var kvp in serviciosConCantidad)
        {
            var servicioId = kvp.Key;
            var cantidad = kvp.Value > 0 ? kvp.Value : 1; // Mínimo 1

            var clienteServicioExistente = serviciosActuales
                .FirstOrDefault(cs => cs.ServicioId == servicioId);

            if (clienteServicioExistente != null)
            {
                // Si existe, actualizar cantidad y reactivar si está desactivado
                clienteServicioExistente.Cantidad = cantidad;
                if (!clienteServicioExistente.Activo)
                {
                    clienteServicioExistente.Activo = true;
                    clienteServicioExistente.FechaInicio = DateTime.Now;
                    clienteServicioExistente.FechaFin = null;
                }
            }
            else
            {
                // Crear nuevo ClienteServicio con cantidad
                var nuevoClienteServicio = new ClienteServicio
                {
                    ClienteId = clienteId,
                    ServicioId = servicioId,
                    Cantidad = cantidad,
                    Activo = true,
                    FechaInicio = DateTime.Now,
                    FechaCreacion = DateTime.Now
                };
                _context.ClienteServicios.Add(nuevoClienteServicio);
            }
        }

        _context.SaveChanges();
    }

    public void ActivarServicio(int clienteId, int servicioId)
    {
        var clienteServicio = _context.ClienteServicios
            .FirstOrDefault(cs => cs.ClienteId == clienteId && cs.ServicioId == servicioId);

        if (clienteServicio == null)
        {
            // Crear nuevo
            clienteServicio = new ClienteServicio
            {
                ClienteId = clienteId,
                ServicioId = servicioId,
                Activo = true,
                FechaInicio = DateTime.Now,
                FechaCreacion = DateTime.Now
            };
            _context.ClienteServicios.Add(clienteServicio);
        }
        else
        {
            // Reactivar si estaba desactivado
            clienteServicio.Activo = true;
            clienteServicio.FechaInicio = DateTime.Now;
            clienteServicio.FechaFin = null;
        }

        _context.SaveChanges();
    }

    public void DesactivarServicio(int clienteId, int servicioId)
    {
        var clienteServicio = _context.ClienteServicios
            .FirstOrDefault(cs => cs.ClienteId == clienteId && cs.ServicioId == servicioId);

        if (clienteServicio != null && clienteServicio.Activo)
        {
            clienteServicio.Activo = false;
            clienteServicio.FechaFin = DateTime.Now;
            _context.SaveChanges();
        }
    }

    public bool TieneServicioActivo(int clienteId, int servicioId)
    {
        return _context.ClienteServicios
            .Any(cs => cs.ClienteId == clienteId && 
                      cs.ServicioId == servicioId && 
                      cs.Activo);
    }
}

