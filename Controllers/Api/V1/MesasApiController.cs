using BarRestPOS.Data;
using BarRestPOS.Models.Api;
using BarRestPOS.Models.Entities;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/mesas")]
public class MesasApiController : BaseApiController
{
    private readonly ApplicationDbContext _context;

    public MesasApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult GetAll([FromQuery] int? ubicacionId, [FromQuery] string? estado, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _context.Mesas
            .AsNoTracking()
            .Include(m => m.Ubicacion)
            .Where(m => m.Activo);

        if (ubicacionId.HasValue) query = query.Where(m => m.UbicacionId == ubicacionId.Value);
        if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(m => m.Estado == estado);

        var total = query.Count();
        var mesas = query
            .OrderBy(m => m.Numero)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.Numero,
                m.Capacidad,
                m.Estado,
                m.UbicacionId,
                Ubicacion = m.Ubicacion != null ? m.Ubicacion.Nombre : null,
                m.Activo
            })
            .ToList();

        var mesaIds = mesas.Select(m => m.Id).ToList();
        var ordenesActivas = _context.Facturas
            .AsNoTracking()
            .Where(f => f.MesaId.HasValue
                        && mesaIds.Contains(f.MesaId.Value)
                        && f.Estado != SD.EstadoOrdenPagado
                        && f.Estado != SD.EstadoOrdenCancelado)
            .GroupBy(f => f.MesaId!.Value)
            .Select(g => new { MesaId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.MesaId, x => x.Count);

        var items = mesas.Select(m => new
        {
            m.Id,
            m.Numero,
            m.Capacidad,
            m.Estado,
            m.UbicacionId,
            m.Ubicacion,
            m.Activo,
            OrdenesActivas = ordenesActivas.TryGetValue(m.Id, out var count) ? count : 0
        });

        return OkResponse(new PagedResult<object>
        {
            Items = items.Cast<object>().ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var mesa = _context.Mesas
            .AsNoTracking()
            .Include(m => m.Ubicacion)
            .FirstOrDefault(m => m.Id == id && m.Activo);

        if (mesa == null) return FailResponse("Mesa no encontrada.", StatusCodes.Status404NotFound);

        return OkResponse(new
        {
            mesa.Id,
            mesa.Numero,
            mesa.Capacidad,
            mesa.Estado,
            mesa.UbicacionId,
            Ubicacion = mesa.Ubicacion?.Nombre,
            mesa.Activo
        });
    }

    [HttpGet("{id:int}/orden-activa")]
    public IActionResult GetOrdenActiva(int id)
    {
        var orden = _context.Facturas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(f => f.FacturaServicios).ThenInclude(x => x.Servicio)
            .Include(f => f.FacturaServicios).ThenInclude(x => x.OpcionesSeleccionadas)
            .Where(f => f.MesaId == id &&
                        f.Estado != SD.EstadoOrdenPagado &&
                        f.Estado != SD.EstadoOrdenCancelado)
            .OrderByDescending(f => f.FechaCreacion)
            .FirstOrDefault();

        if (orden == null) return OkResponse<object?>(null, "Sin orden activa");

        return OkResponse(new
        {
            orden.Id,
            orden.Numero,
            orden.Estado,
            orden.EstadoCocina,
            orden.Monto,
            orden.FechaCreacion,
            Items = orden.FacturaServicios.Select(i => new
            {
                i.Id,
                i.ServicioId,
                Servicio = i.Servicio.Nombre,
                i.Cantidad,
                i.PrecioUnitario,
                i.Monto,
                i.Estado,
                i.Notas,
                opcionesResumen = ProductoOpcionesLineaHelper.OpcionesResumen(i.OpcionesSeleccionadas),
                opcionesSeleccionadas = ProductoOpcionesLineaHelper.MapOpcionesLineaRespuesta(i.OpcionesSeleccionadas)
            })
        });
    }

    [HttpPost]
    [Authorize(Policy = "Administrador")]
    public IActionResult Create([FromBody] MesaUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Numero))
            return FailResponse("Número de mesa es requerido.");

        var existe = _context.Mesas.Any(m => m.Numero == request.Numero && m.Activo);
        if (existe) return FailResponse("Ya existe una mesa con ese número.");

        var mesa = new Mesa
        {
            Numero = request.Numero.Trim(),
            Capacidad = request.Capacidad <= 0 ? 4 : request.Capacidad,
            Estado = string.IsNullOrWhiteSpace(request.Estado) ? SD.EstadoMesaLibre : request.Estado.Trim(),
            UbicacionId = request.UbicacionId,
            Activo = true
        };

        _context.Mesas.Add(mesa);
        _context.SaveChanges();
        return OkResponse(new { mesa.Id }, "Mesa creada");
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult Update(int id, [FromBody] MesaUpsertRequest request)
    {
        var mesa = _context.Mesas.FirstOrDefault(m => m.Id == id && m.Activo);
        if (mesa == null) return FailResponse("Mesa no encontrada.", StatusCodes.Status404NotFound);

        if (!string.IsNullOrWhiteSpace(request.Numero) &&
            _context.Mesas.Any(m => m.Id != id && m.Numero == request.Numero && m.Activo))
        {
            return FailResponse("Ya existe una mesa con ese número.");
        }

        if (!string.IsNullOrWhiteSpace(request.Numero)) mesa.Numero = request.Numero.Trim();
        if (request.Capacidad > 0) mesa.Capacidad = request.Capacidad;
        if (!string.IsNullOrWhiteSpace(request.Estado)) mesa.Estado = request.Estado.Trim();
        mesa.UbicacionId = request.UbicacionId;

        _context.SaveChanges();
        return OkResponse(new { mesa.Id }, "Mesa actualizada");
    }

    [HttpPatch("{id:int}/estado")]
    public IActionResult CambiarEstado(int id, [FromBody] CambiarEstadoMesaRequest request)
    {
        var mesa = _context.Mesas.FirstOrDefault(m => m.Id == id && m.Activo);
        if (mesa == null) return FailResponse("Mesa no encontrada.", StatusCodes.Status404NotFound);
        if (string.IsNullOrWhiteSpace(request.Estado)) return FailResponse("Estado es requerido.");

        mesa.Estado = request.Estado.Trim();
        _context.SaveChanges();
        return OkResponse(new { mesa.Id, mesa.Estado }, "Estado actualizado");
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult Delete(int id)
    {
        var mesa = _context.Mesas.FirstOrDefault(m => m.Id == id && m.Activo);
        if (mesa == null) return FailResponse("Mesa no encontrada.", StatusCodes.Status404NotFound);

        mesa.Activo = false;
        _context.SaveChanges();
        return OkResponse(new { mesa.Id }, "Mesa desactivada");
    }
}

public class MesaUpsertRequest
{
    public string Numero { get; set; } = string.Empty;
    public int Capacidad { get; set; } = 4;
    public string Estado { get; set; } = SD.EstadoMesaLibre;
    public int? UbicacionId { get; set; }
}

public class CambiarEstadoMesaRequest
{
    public string Estado { get; set; } = string.Empty;
}
