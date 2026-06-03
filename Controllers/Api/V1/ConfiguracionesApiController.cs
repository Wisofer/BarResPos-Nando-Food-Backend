using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/configuraciones")]
public class ConfiguracionesApiController : BaseApiController
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguracionService _configuracionService;

    public ConfiguracionesApiController(ApplicationDbContext context, IConfiguracionService configuracionService)
    {
        _context = context;
        _configuracionService = configuracionService;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var items = _context.Configuraciones
            .OrderBy(c => c.Clave)
            .Select(c => new
            {
                c.Id,
                c.Clave,
                c.Valor,
                c.Descripcion,
                c.FechaActualizacion
            })
            .ToList();
        return OkResponse(items);
    }

    [HttpGet("tipo-cambio")]
    public IActionResult GetTipoCambio()
    {
        var tipoCambio = _configuracionService.ObtenerValorDecimal("TipoCambioDolar");
        return OkResponse(new { TipoCambioDolar = tipoCambio });
    }

    [HttpPut("tipo-cambio")]
    [Authorize(Policy = "Administrador")]
    public IActionResult UpdateTipoCambio([FromBody] UpdateTipoCambioRequest request)
    {
        if (request.TipoCambioDolar <= 0) return FailResponse("Tipo de cambio inválido.");

        _configuracionService.ActualizarValor(
            "TipoCambioDolar",
            request.TipoCambioDolar.ToString("F2"),
            User.Identity?.Name ?? "sistema");

        return OkResponse(new { request.TipoCambioDolar }, "Tipo de cambio actualizado");
    }

    [HttpPut("{clave}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult Upsert(string clave, [FromBody] UpsertConfiguracionRequest request)
    {
        if (string.IsNullOrWhiteSpace(clave)) return FailResponse("Clave inválida.");
        if (string.IsNullOrWhiteSpace(request.Valor)) return FailResponse("Valor es requerido.");

        var config = _context.Configuraciones.FirstOrDefault(c => c.Clave == clave);
        if (config == null)
        {
            config = new Configuracion
            {
                Clave = clave.Trim(),
                Valor = request.Valor.Trim(),
                Descripcion = request.Descripcion?.Trim(),
                FechaCreacion = DateTime.Now,
                FechaActualizacion = DateTime.Now,
                UsuarioActualizacion = User.Identity?.Name
            };
            _context.Configuraciones.Add(config);
        }
        else
        {
            config.Valor = request.Valor.Trim();
            config.Descripcion = request.Descripcion?.Trim();
            config.FechaActualizacion = DateTime.Now;
            config.UsuarioActualizacion = User.Identity?.Name;
        }

        _context.SaveChanges();
        return OkResponse(new { config.Id, config.Clave, config.Valor }, "Configuración guardada");
    }

    [HttpGet("plantillas-whatsapp")]
    public IActionResult GetPlantillasWhatsApp([FromQuery] bool? activas)
    {
        var query = _context.PlantillasMensajeWhatsApp.AsQueryable();
        if (activas.HasValue) query = query.Where(p => p.Activa == activas.Value);

        var items = query
            .OrderByDescending(p => p.EsDefault)
            .ThenBy(p => p.Nombre)
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Mensaje,
                p.Activa,
                p.EsDefault,
                p.FechaCreacion,
                p.FechaActualizacion
            })
            .ToList();

        return OkResponse(items);
    }

    [HttpGet("plantillas-whatsapp/{id:int}")]
    public IActionResult GetPlantillaWhatsAppById(int id)
    {
        var item = _context.PlantillasMensajeWhatsApp
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Mensaje,
                p.Activa,
                p.EsDefault,
                p.FechaCreacion,
                p.FechaActualizacion
            })
            .FirstOrDefault();

        if (item == null) return FailResponse("Plantilla no encontrada.", StatusCodes.Status404NotFound);
        return OkResponse(item);
    }

    [HttpPost("plantillas-whatsapp")]
    [Authorize(Policy = "Administrador")]
    public IActionResult CrearPlantillaWhatsApp([FromBody] PlantillaWhatsAppUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Mensaje))
            return FailResponse("Nombre y mensaje son requeridos.");

        if (_context.PlantillasMensajeWhatsApp.Any(p => p.Nombre.ToLower() == request.Nombre.Trim().ToLower()))
            return FailResponse("Ya existe una plantilla con ese nombre.");

        if (request.EsDefault)
        {
            var otrasDefault = _context.PlantillasMensajeWhatsApp.Where(p => p.EsDefault).ToList();
            foreach (var p in otrasDefault) p.EsDefault = false;
        }

        var plantilla = new PlantillaMensajeWhatsApp
        {
            Nombre = request.Nombre.Trim(),
            Mensaje = request.Mensaje.Trim(),
            Activa = request.Activa,
            EsDefault = request.EsDefault,
            FechaCreacion = DateTime.Now
        };

        _context.PlantillasMensajeWhatsApp.Add(plantilla);
        _context.SaveChanges();
        return OkResponse(new { plantilla.Id }, "Plantilla creada");
    }

    [HttpPut("plantillas-whatsapp/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult ActualizarPlantillaWhatsApp(int id, [FromBody] PlantillaWhatsAppUpsertRequest request)
    {
        var plantilla = _context.PlantillasMensajeWhatsApp.FirstOrDefault(p => p.Id == id);
        if (plantilla == null) return FailResponse("Plantilla no encontrada.", StatusCodes.Status404NotFound);

        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Mensaje))
            return FailResponse("Nombre y mensaje son requeridos.");

        var nombre = request.Nombre.Trim();
        if (_context.PlantillasMensajeWhatsApp.Any(p => p.Id != id && p.Nombre.ToLower() == nombre.ToLower()))
            return FailResponse("Ya existe otra plantilla con ese nombre.");

        if (request.EsDefault && !plantilla.EsDefault)
        {
            var otrasDefault = _context.PlantillasMensajeWhatsApp.Where(p => p.EsDefault && p.Id != id).ToList();
            foreach (var p in otrasDefault) p.EsDefault = false;
        }

        plantilla.Nombre = nombre;
        plantilla.Mensaje = request.Mensaje.Trim();
        plantilla.Activa = request.Activa;
        plantilla.EsDefault = request.EsDefault;
        plantilla.FechaActualizacion = DateTime.Now;

        _context.SaveChanges();
        return OkResponse(new { plantilla.Id }, "Plantilla actualizada");
    }

    [HttpDelete("plantillas-whatsapp/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult EliminarPlantillaWhatsApp(int id)
    {
        var plantilla = _context.PlantillasMensajeWhatsApp.FirstOrDefault(p => p.Id == id);
        if (plantilla == null) return FailResponse("Plantilla no encontrada.", StatusCodes.Status404NotFound);

        if (plantilla.EsDefault)
        {
            var existeOtraDefault = _context.PlantillasMensajeWhatsApp.Any(p => p.EsDefault && p.Id != id);
            if (!existeOtraDefault)
                return FailResponse("No se puede eliminar la única plantilla por defecto.");
        }

        _context.PlantillasMensajeWhatsApp.Remove(plantilla);
        _context.SaveChanges();
        return OkResponse(new { id }, "Plantilla eliminada");
    }

    [HttpPatch("plantillas-whatsapp/{id:int}/marcar-default")]
    [Authorize(Policy = "Administrador")]
    public IActionResult MarcarPlantillaWhatsAppDefault(int id)
    {
        var plantilla = _context.PlantillasMensajeWhatsApp.FirstOrDefault(p => p.Id == id);
        if (plantilla == null) return FailResponse("Plantilla no encontrada.", StatusCodes.Status404NotFound);

        var otrasDefault = _context.PlantillasMensajeWhatsApp.Where(p => p.EsDefault && p.Id != id).ToList();
        foreach (var p in otrasDefault) p.EsDefault = false;

        plantilla.EsDefault = true;
        plantilla.FechaActualizacion = DateTime.Now;
        _context.SaveChanges();

        return OkResponse(new { plantilla.Id, plantilla.EsDefault }, "Plantilla marcada como predeterminada");
    }

    [HttpPost("logo")]
    [Authorize(Policy = "Administrador")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirLogo([FromForm] IFormFile archivo)
    {
        if (archivo == null || archivo.Length <= 0)
            return FailResponse("Debe adjuntar una imagen válida.");
        if (archivo.Length > 5 * 1024 * 1024)
            return FailResponse("La imagen no debe superar 5MB.");

        var tiposPermitidos = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!tiposPermitidos.Contains((archivo.ContentType ?? string.Empty).ToLowerInvariant()))
            return FailResponse("Formato no permitido. Use JPG, PNG o WEBP.");

        var appDataPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "BarRestPOS");
        var uploadsFolder = System.IO.Path.Combine(appDataPath, "uploads", "logo");
        if (!System.IO.Directory.Exists(uploadsFolder))
        {
            System.IO.Directory.CreateDirectory(uploadsFolder);
        }

        var extension = System.IO.Path.GetExtension(archivo.FileName);
        if (string.IsNullOrEmpty(extension)) extension = ".png";
        var fileName = $"logo_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
        var filePath = System.IO.Path.Combine(uploadsFolder, fileName);

        await using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
        {
            await archivo.CopyToAsync(fileStream);
        }

        var relativeUrl = $"/uploads/logo/{fileName}";

        var config = _context.Configuraciones.FirstOrDefault(c => c.Clave == "Tickets:LogoUrl");
        if (config == null)
        {
            config = new Configuracion
            {
                Clave = "Tickets:LogoUrl",
                Valor = relativeUrl,
                Descripcion = "URL relativa para el logo de los tickets (impresión y PDF)",
                FechaCreacion = DateTime.Now,
                FechaActualizacion = DateTime.Now,
                UsuarioActualizacion = User.Identity?.Name
            };
            _context.Configuraciones.Add(config);
        }
        else
        {
            config.Valor = relativeUrl;
            config.FechaActualizacion = DateTime.Now;
            config.UsuarioActualizacion = User.Identity?.Name;
        }

        _context.SaveChanges();

        return OkResponse(new
        {
            LogoUrl = relativeUrl
        }, "Logo subido y configurado correctamente.");
    }
}

public class UpdateTipoCambioRequest
{
    public decimal TipoCambioDolar { get; set; }
}

public class UpsertConfiguracionRequest
{
    public string Valor { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

public class PlantillaWhatsAppUpsertRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public bool EsDefault { get; set; } = false;
}
