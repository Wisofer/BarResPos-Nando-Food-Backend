using BarRestPOS.Data;
using BarRestPOS.Models.Api;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/productos")]
public class ProductosApiController : BaseApiController
{
    private readonly ApplicationDbContext _context;
    private readonly IInventarioService _inventarioService;
    private readonly ExcelExportService _excelExportService;
    private readonly IR2StorageService _r2StorageService;

    public ProductosApiController(
        ApplicationDbContext context,
        IInventarioService inventarioService,
        ExcelExportService excelExportService,
        IR2StorageService r2StorageService)
    {
        _context = context;
        _inventarioService = inventarioService;
        _excelExportService = excelExportService;
        _r2StorageService = r2StorageService;
    }

    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] string? search,
        [FromQuery] int? categoriaId,
        [FromQuery] bool? activos,
        [FromQuery] bool incluirOpciones = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _context.Servicios
            .AsNoTracking()
            .Include(s => s.CategoriaProducto)
            .AsQueryable();

        if (incluirOpciones)
            query = query
                .Include(s => s.OpcionGrupos.Where(g => g.Activo))
                .ThenInclude(g => g.Opciones.Where(o => o.Activo));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(s => s.Nombre.ToLower().Contains(q) || (s.Codigo != null && s.Codigo.ToLower().Contains(q)));
        }

        if (categoriaId.HasValue) query = query.Where(s => s.CategoriaProductoId == categoriaId.Value);
        if (activos.HasValue) query = query.Where(s => s.Activo == activos.Value);

        var total = query.Count();
        var pageQuery = query.OrderBy(s => s.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        List<object> items;
        if (incluirOpciones)
        {
            items = pageQuery
                .AsSplitQuery()
                .ToList()
                .Select(s => (object)new
                {
                    s.Id,
                    s.Codigo,
                    s.Nombre,
                    s.Descripcion,
                    s.Precio,
                    PrecioVenta = s.Precio,
                    s.PrecioCompra,
                    s.Categoria,
                    s.CategoriaProductoId,
                    CategoriaProducto = s.CategoriaProducto != null ? s.CategoriaProducto.Nombre : null,
                    s.Stock,
                    s.StockMinimo,
                    s.ControlarStock,
                    s.EsPreparado,
                    s.ImagenUrl,
                    s.Destacado,
                    s.Activo,
                    opcionesGrupos = ProductoOpcionesLineaHelper.MapOpcionesGruposCatalogo(s.OpcionGrupos)
                })
                .ToList();
        }
        else
        {
            items = pageQuery
                .Select(s => new
                {
                    s.Id,
                    s.Codigo,
                    s.Nombre,
                    s.Descripcion,
                    s.Precio,
                    PrecioVenta = s.Precio,
                    s.PrecioCompra,
                    s.Categoria,
                    s.CategoriaProductoId,
                    CategoriaProducto = s.CategoriaProducto != null ? s.CategoriaProducto.Nombre : null,
                    s.Stock,
                    s.StockMinimo,
                    s.ControlarStock,
                    s.EsPreparado,
                    s.ImagenUrl,
                    s.Destacado,
                    s.Activo
                })
                .Cast<object>()
                .ToList();
        }

        return OkResponse(new PagedResult<object>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var s = _context.Servicios
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.CategoriaProducto)
            .Include(x => x.OpcionGrupos.Where(g => g.Activo))
            .ThenInclude(g => g.Opciones.Where(o => o.Activo))
            .FirstOrDefault(x => x.Id == id);

        if (s == null) return FailResponse("Producto no encontrado.", StatusCodes.Status404NotFound);

        var item = new
        {
            s.Id,
            s.Codigo,
            s.Nombre,
            s.Descripcion,
            s.Precio,
            PrecioVenta = s.Precio,
            s.PrecioCompra,
            s.Categoria,
            s.CategoriaProductoId,
            CategoriaProducto = s.CategoriaProducto != null ? s.CategoriaProducto.Nombre : null,
            s.Stock,
            s.StockMinimo,
            s.ControlarStock,
            s.EsPreparado,
            s.ImagenUrl,
            s.Destacado,
            s.Activo,
            opcionesGrupos = ProductoOpcionesLineaHelper.MapOpcionesGruposCatalogo(s.OpcionGrupos)
        };

        return OkResponse(item);
    }

    [HttpPost]
    [Authorize(Policy = "Administrador")]
    public IActionResult Create([FromBody] ProductoUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre)) return FailResponse("Nombre es requerido.");
        if (request.GetPrecioVenta() < 0) return FailResponse("Precio de venta inválido.");
        if (request.PrecioCompra < 0) return FailResponse("Precio de compra inválido.");

        if (!string.IsNullOrWhiteSpace(request.Codigo) && _context.Servicios.Any(s => s.Codigo == request.Codigo))
            return FailResponse("Código de producto ya existe.");

        var producto = new Servicio
        {
            Codigo = request.Codigo?.Trim() ?? string.Empty,
            Nombre = request.Nombre.Trim(),
            Descripcion = request.Descripcion?.Trim(),
            Precio = request.GetPrecioVenta(),
            PrecioCompra = request.PrecioCompra,
            Categoria = string.IsNullOrWhiteSpace(request.Categoria) ? "General" : request.Categoria.Trim(),
            CategoriaProductoId = request.CategoriaProductoId,
            Stock = request.Stock,
            StockMinimo = request.StockMinimo,
            ControlarStock = request.ControlarStock,
            EsPreparado = request.EsPreparado ?? true,
            ImagenUrl = request.ImagenUrl,
            Destacado = request.Destacado,
            Activo = request.Activo
        };

        _context.Servicios.Add(producto);
        _context.SaveChanges();
        return OkResponse(new { producto.Id }, "Producto creado");
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult Update(int id, [FromBody] ProductoUpsertRequest request)
    {
        var producto = _context.Servicios.FirstOrDefault(s => s.Id == id);
        if (producto == null) return FailResponse("Producto no encontrado.", StatusCodes.Status404NotFound);

        if (!string.IsNullOrWhiteSpace(request.Codigo) && _context.Servicios.Any(s => s.Id != id && s.Codigo == request.Codigo))
            return FailResponse("Código de producto ya existe.");
        if (request.GetPrecioVenta() < 0) return FailResponse("Precio de venta inválido.");
        if (request.PrecioCompra < 0) return FailResponse("Precio de compra inválido.");

        if (!string.IsNullOrWhiteSpace(request.Codigo)) producto.Codigo = request.Codigo.Trim();
        if (!string.IsNullOrWhiteSpace(request.Nombre)) producto.Nombre = request.Nombre.Trim();
        producto.Descripcion = request.Descripcion?.Trim();
        producto.Precio = request.GetPrecioVenta();
        producto.PrecioCompra = request.PrecioCompra;
        if (!string.IsNullOrWhiteSpace(request.Categoria)) producto.Categoria = request.Categoria.Trim();
        producto.CategoriaProductoId = request.CategoriaProductoId;
        producto.Stock = request.Stock;
        producto.StockMinimo = request.StockMinimo;
        producto.ControlarStock = request.ControlarStock;
        if (request.EsPreparado.HasValue) producto.EsPreparado = request.EsPreparado.Value;
        producto.ImagenUrl = request.ImagenUrl;
        producto.Destacado = request.Destacado;
        producto.Activo = request.Activo;

        _context.SaveChanges();
        return OkResponse(new { producto.Id }, "Producto actualizado");
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult Delete(int id)
    {
        var producto = _context.Servicios.FirstOrDefault(s => s.Id == id);
        if (producto == null) return FailResponse("Producto no encontrado.", StatusCodes.Status404NotFound);

        producto.Activo = false;
        _context.SaveChanges();
        return OkResponse(new { producto.Id }, "Producto desactivado");
    }

    /// <summary>
    /// Sube imagen de producto a Cloudflare R2. Solo aplica para productos de comida (EsPreparado=true).
    /// </summary>
    [HttpPost("{id:int}/imagen")]
    [Authorize(Policy = "Administrador")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirImagen(int id, [FromForm] IFormFile archivo)
    {
        var producto = _context.Servicios.FirstOrDefault(s => s.Id == id);
        if (producto == null) return FailResponse("Producto no encontrado.", StatusCodes.Status404NotFound);
        if (!producto.EsPreparado)
            return FailResponse("Solo se permite subir imagen para productos de comida (esPreparado=true).", StatusCodes.Status400BadRequest);
        if (archivo == null || archivo.Length <= 0)
            return FailResponse("Debe adjuntar una imagen válida.");
        if (archivo.Length > 5 * 1024 * 1024)
            return FailResponse("La imagen no debe superar 5MB.");

        var tiposPermitidos = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!tiposPermitidos.Contains((archivo.ContentType ?? string.Empty).ToLowerInvariant()))
            return FailResponse("Formato no permitido. Use JPG, PNG o WEBP.");

        await using var stream = archivo.OpenReadStream();
        var contentType = string.IsNullOrWhiteSpace(archivo.ContentType) ? "image/jpeg" : archivo.ContentType;
        var imageUrl = await _r2StorageService.UploadProductImageAsync(
            producto.Id,
            stream,
            archivo.FileName,
            contentType);

        producto.ImagenUrl = imageUrl;
        _context.SaveChanges();

        return OkResponse(new
        {
            producto.Id,
            producto.Nombre,
            producto.ImagenUrl
        }, "Imagen subida correctamente.");
    }

    [HttpGet("exportar-excel")]
    [Authorize(Policy = "Administrador")]
    public IActionResult ExportarExcel([FromQuery] string? search, [FromQuery] int? categoriaId, [FromQuery] bool? activos)
    {
        var query = _context.Servicios
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(s => s.Nombre.ToLower().Contains(q) || (s.Codigo != null && s.Codigo.ToLower().Contains(q)));
        }

        if (categoriaId.HasValue) query = query.Where(s => s.CategoriaProductoId == categoriaId.Value);
        if (activos.HasValue) query = query.Where(s => s.Activo == activos.Value);

        var productosBase = query
            .OrderBy(s => s.Nombre)
            .Select(s => new
            {
                s.Id,
                s.Codigo,
                s.Nombre,
                s.Categoria,
                s.PrecioCompra,
                s.Precio,
                s.Stock,
                s.StockMinimo,
                s.ControlarStock,
                s.Activo
            })
            .ToList();

        var productoIds = productosBase.Select(p => p.Id).ToList();
        var proveedoresPorProducto = _context.MovimientosInventario
            .AsNoTracking()
            .Include(m => m.Proveedor)
            .Where(m => productoIds.Contains(m.ProductoId) && m.ProveedorId.HasValue)
            .OrderByDescending(m => m.Fecha)
            .ToList()
            .GroupBy(m => m.ProductoId)
            .ToDictionary(g => g.Key, g => g.First().Proveedor?.Nombre ?? string.Empty);

        var productos = productosBase
            .Select(p => (dynamic)new
            {
                p.Codigo,
                p.Nombre,
                p.Categoria,
                Proveedor = proveedoresPorProducto.TryGetValue(p.Id, out var proveedor) ? proveedor : string.Empty,
                p.PrecioCompra,
                p.Precio,
                p.Stock,
                p.StockMinimo,
                p.ControlarStock,
                p.Activo
            })
            .ToList();

        var excel = _excelExportService.ExportarProductos(productos);
        var nombre = $"productos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }

    [HttpPost("entrada-stock")]
    [Authorize(Policy = "Administrador")]
    public IActionResult RegistrarEntradaStock([FromBody] RegistrarEntradaStockRequest request)
    {
        if (request.ProductoId <= 0) return FailResponse("ProductoId es requerido.");
        if (request.Cantidad <= 0) return FailResponse("Cantidad debe ser mayor a 0.");
        if (request.CostoUnitario.HasValue && request.CostoUnitario.Value < 0) return FailResponse("CostoUnitario inválido.");

        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) return FailResponse("Usuario no autenticado.", StatusCodes.Status401Unauthorized);

        try
        {
            var movimiento = _inventarioService.RegistrarEntrada(
                request.ProductoId,
                request.Cantidad,
                request.CostoUnitario,
                request.ProveedorId,
                request.NumeroFactura,
                request.Observaciones,
                userId.Value
            );

            return OkResponse(new
            {
                movimiento.Id,
                movimiento.ProductoId,
                movimiento.Tipo,
                movimiento.Subtipo,
                movimiento.Cantidad,
                movimiento.StockAnterior,
                movimiento.StockNuevo,
                movimiento.Fecha
            }, "Entrada de inventario registrada.");
        }
        catch (Exception ex)
        {
            return FailResponse($"Error al registrar entrada: {ex.Message}");
        }
    }

    [HttpPost("salida-stock")]
    [Authorize(Policy = "Administrador")]
    public IActionResult RegistrarSalidaStock([FromBody] RegistrarSalidaStockRequest request)
    {
        if (request.ProductoId <= 0) return FailResponse("ProductoId es requerido.");
        if (request.Cantidad <= 0) return FailResponse("Cantidad debe ser mayor a 0.");
        if (string.IsNullOrWhiteSpace(request.Subtipo)) return FailResponse("Subtipo es requerido.");

        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) return FailResponse("Usuario no autenticado.", StatusCodes.Status401Unauthorized);

        try
        {
            var movimiento = _inventarioService.RegistrarSalida(
                request.ProductoId,
                request.Cantidad,
                request.Subtipo.Trim(),
                null,
                request.Observaciones,
                userId.Value
            );

            return OkResponse(new
            {
                movimiento.Id,
                movimiento.ProductoId,
                movimiento.Tipo,
                movimiento.Subtipo,
                movimiento.Cantidad,
                movimiento.StockAnterior,
                movimiento.StockNuevo,
                movimiento.Fecha
            }, "Salida de inventario registrada.");
        }
        catch (Exception ex)
        {
            return FailResponse($"Error al registrar salida: {ex.Message}");
        }
    }

    [HttpPost("ajuste-stock")]
    [Authorize(Policy = "Administrador")]
    public IActionResult RegistrarAjusteStock([FromBody] RegistrarAjusteStockRequest request)
    {
        if (request.ProductoId <= 0) return FailResponse("ProductoId es requerido.");
        if (request.CantidadNueva < 0) return FailResponse("CantidadNueva no puede ser negativa.");

        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) return FailResponse("Usuario no autenticado.", StatusCodes.Status401Unauthorized);

        try
        {
            var movimiento = _inventarioService.RegistrarAjuste(
                request.ProductoId,
                request.CantidadNueva,
                request.Observaciones,
                userId.Value
            );

            return OkResponse(new
            {
                movimiento.Id,
                movimiento.ProductoId,
                movimiento.Tipo,
                movimiento.Subtipo,
                movimiento.Cantidad,
                movimiento.StockAnterior,
                movimiento.StockNuevo,
                movimiento.Fecha
            }, "Ajuste de inventario registrado.");
        }
        catch (Exception ex)
        {
            return FailResponse($"Error al registrar ajuste: {ex.Message}");
        }
    }

    [HttpGet("{id:int}/movimientos")]
    public IActionResult GetMovimientosProducto(int id, [FromQuery] int? limite)
    {
        var producto = _context.Servicios.AsNoTracking().FirstOrDefault(s => s.Id == id);
        if (producto == null) return FailResponse("Producto no encontrado.", StatusCodes.Status404NotFound);

        if (limite.HasValue && limite.Value <= 0) return FailResponse("Limite debe ser mayor a 0.");

        var movimientos = _inventarioService.ObtenerHistorial(id, limite).Select(m => new
        {
            m.Id,
            m.ProductoId,
            m.Tipo,
            m.Subtipo,
            m.Cantidad,
            m.CostoUnitario,
            m.CostoTotal,
            m.Fecha,
            Usuario = m.Usuario != null ? m.Usuario.NombreCompleto : null,
            Proveedor = m.Proveedor != null ? m.Proveedor.Nombre : null,
            m.NumeroFactura,
            m.Observaciones,
            m.StockAnterior,
            m.StockNuevo
        }).ToList();

        return OkResponse(new
        {
            Producto = new { producto.Id, producto.Nombre, producto.Stock, producto.ControlarStock },
            Movimientos = movimientos
        });
    }

    [HttpGet("movimientos")]
    [Authorize(Policy = "Administrador")]
    public IActionResult GetMovimientos([FromQuery] int? productoId, [FromQuery] DateTime? fechaInicio, [FromQuery] DateTime? fechaFin, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _context.MovimientosInventario
            .AsNoTracking()
            .Include(m => m.Producto)
            .Include(m => m.Usuario)
            .Include(m => m.Proveedor)
            .AsQueryable();

        if (productoId.HasValue) query = query.Where(m => m.ProductoId == productoId.Value);
        if (fechaInicio.HasValue) query = query.Where(m => m.Fecha >= fechaInicio.Value);
        if (fechaFin.HasValue) query = query.Where(m => m.Fecha <= fechaFin.Value);

        var total = query.Count();
        var pageItems = query.OrderByDescending(m => m.Fecha)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.ProductoId,
                ProductoJoin = m.Producto != null ? m.Producto.Nombre : null,
                m.Tipo,
                m.Subtipo,
                m.Cantidad,
                m.CostoUnitario,
                m.CostoTotal,
                m.Fecha,
                Usuario = m.Usuario != null ? m.Usuario.NombreCompleto : null,
                Proveedor = m.Proveedor != null ? m.Proveedor.Nombre : null,
                m.NumeroFactura,
                m.Observaciones,
                m.StockAnterior,
                m.StockNuevo
            })
            .ToList();

        // Fallback robusto para históricos con relación rota o join nulo:
        // 1) intenta con nombre por join, 2) intenta lookup por ProductoId, 3) usa "Producto #id".
        var idsProductos = pageItems.Select(i => i.ProductoId).Distinct().ToList();
        var nombresPorId = _context.Servicios
            .AsNoTracking()
            .Where(s => idsProductos.Contains(s.Id))
            .Select(s => new { s.Id, s.Nombre })
            .ToDictionary(x => x.Id, x => x.Nombre);

        var items = pageItems.Select(i =>
        {
            var nombreProducto = !string.IsNullOrWhiteSpace(i.ProductoJoin)
                ? i.ProductoJoin
                : (nombresPorId.TryGetValue(i.ProductoId, out var n) ? n : $"Producto #{i.ProductoId}");

            return new
            {
                i.Id,
                i.ProductoId,
                Producto = nombreProducto,
                productoNombre = nombreProducto,
                i.Tipo,
                i.Subtipo,
                i.Cantidad,
                i.CostoUnitario,
                i.CostoTotal,
                i.Fecha,
                i.Usuario,
                i.Proveedor,
                i.NumeroFactura,
                i.Observaciones,
                i.StockAnterior,
                i.StockNuevo
            };
        }).ToList();

        return OkResponse(new PagedResult<object>
        {
            Items = items.Cast<object>().ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    [HttpGet("movimientos/excel")]
    [Authorize(Policy = "Administrador")]
    public IActionResult GetMovimientosExcel([FromQuery] int? productoId, [FromQuery] DateTime? fechaInicio, [FromQuery] DateTime? fechaFin)
    {
        var query = _context.MovimientosInventario
            .AsNoTracking()
            .Include(m => m.Producto)
            .Include(m => m.Usuario)
            .OrderByDescending(m => m.Fecha)
            .AsQueryable();

        if (productoId.HasValue) query = query.Where(m => m.ProductoId == productoId.Value);
        if (fechaInicio.HasValue) query = query.Where(m => m.Fecha >= fechaInicio.Value);
        if (fechaFin.HasValue) query = query.Where(m => m.Fecha <= fechaFin.Value);

        var movimientos = query.ToList();
        
        var items = movimientos.Select(m => new
        {
            Fecha = m.Fecha,
            Producto = m.Producto?.Nombre ?? "Eliminado",
            Tipo = m.Tipo,
            Subtipo = m.Subtipo,
            Cantidad = m.Cantidad,
            StockAnterior = m.StockAnterior,
            StockNuevo = m.StockNuevo,
            Usuario = m.Usuario?.NombreCompleto ?? "Sistema",
            Observaciones = m.Observaciones
        }).ToList();

        var excelBytes = _excelExportService.ExportarMovimientosInventario(items);
        var nombreArchivo = $"movimientos_inventario_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
    }
}

public class ProductoUpsertRequest
{
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    // Compatibilidad: si frontend envía solo "precio", se usa como precio de venta.
    public decimal Precio { get; set; } = 0;
    public decimal? PrecioVenta { get; set; }
    public decimal PrecioCompra { get; set; } = 0;
    public string Categoria { get; set; } = "General";
    public int? CategoriaProductoId { get; set; }
    public int Stock { get; set; }
    public int StockMinimo { get; set; }
    public bool ControlarStock { get; set; }
    /// <summary>true = comida preparada (no devuelve stock al cancelar). false = bebida embotellada, etc. Null = mantener default (true al crear).</summary>
    public bool? EsPreparado { get; set; }
    public string? ImagenUrl { get; set; }
    public bool Destacado { get; set; }
    public bool Activo { get; set; } = true;

    public decimal GetPrecioVenta() => PrecioVenta ?? Precio;
}

public class RegistrarEntradaStockRequest
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal? CostoUnitario { get; set; }
    public int? ProveedorId { get; set; }
    public string? NumeroFactura { get; set; }
    public string? Observaciones { get; set; }
}

public class RegistrarSalidaStockRequest
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public string Subtipo { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
}

public class RegistrarAjusteStockRequest
{
    public int ProductoId { get; set; }
    public int CantidadNueva { get; set; }
    public string? Observaciones { get; set; }
}
