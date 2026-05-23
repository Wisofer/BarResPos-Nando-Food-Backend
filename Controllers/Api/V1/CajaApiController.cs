using BarRestPOS.Services;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/caja")]
public class CajaApiController : BaseApiController
{
    private readonly ICajaService _cajaService;
    private readonly ExcelExportService _excelExportService;

    public CajaApiController(ICajaService cajaService, ExcelExportService excelExportService)
    {
        _cajaService = cajaService;
        _excelExportService = excelExportService;
    }

    [HttpGet("estado")]
    public async Task<IActionResult> Estado()
    {
        try
        {
            var response = await _cajaService.ObtenerEstadoActualAsync();
            return OkResponse(new
            {
                response.Abierta,
                Cierre = response.Cierre == null ? null : new
                {
                    response.Cierre.Id,
                    response.Cierre.FechaCierre,
                    response.Cierre.Estado,
                    response.Cierre.MontoInicial,
                    Usuario = response.Cierre.Usuario != null ? response.Cierre.Usuario.NombreCompleto : null
                }
            });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("ordenes-pendientes")]
    public async Task<IActionResult> OrdenesPendientes()
    {
        try
        {
            var items = await _cajaService.ObtenerOrdenesPendientesAsync();
            return OkResponse(items);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("apertura")]
    [Authorize(Policy = "Administrador")]
    public async Task<IActionResult> Apertura([FromBody] AperturaCajaRequest request)
    {
        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) return FailResponse("Usuario no válido.", StatusCodes.Status401Unauthorized);

        try
        {
            var cierre = await _cajaService.AbrirCajaAsync(request.MontoInicial, userId.Value);
            return OkResponse(new
            {
                cierre.Id,
                cierre.Estado,
                cierre.MontoInicial,
                cierre.MontoEsperado
            }, "Caja abierta");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("cierre/preview")]
    [Authorize(Policy = "Administrador")]
    public async Task<IActionResult> PreviewCierre()
    {
        try
        {
            var preview = await _cajaService.ObtenerPreviewCierreAsync();
            return OkResponse(new
            {
                Cierre = new
                {
                    Id = preview.CierreId,
                    preview.FechaCierre,
                    preview.Estado,
                    MontoInicial = preview.MontoInicial
                },
                Totales = new
                {
                    preview.TotalVentasNetas,
                    preview.TotalEfectivo,
                    preview.TotalTarjeta,
                    preview.TotalTransferencia,
                    preview.TotalCordobas,
                    preview.TotalDolares,
                    preview.TotalGeneral,
                    preview.TotalOrdenes,
                    preview.TotalPagos,
                    preview.MontoInicial,
                    preview.MontoEsperado,
                    DescripcionMontoEsperado = "Monto inicial de apertura + totalEfectivo. El efectivo es el neto retenido (Pago.Monto en efectivo; no el billete entregado si hubo vuelto). No se suma el monto inicial dos veces."
                }
            });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("cierre")]
    [Authorize(Policy = "Administrador")]
    public async Task<IActionResult> CerrarCaja([FromBody] CierreCajaRequest request)
    {
        try
        {
            var cierre = await _cajaService.CerrarCajaAsync(request.MontoReal, request.Observaciones);
            return OkResponse(new
            {
                cierre.Id,
                cierre.Estado,
                cierre.MontoEsperado,
                cierre.MontoReal,
                cierre.Diferencia
            }, "Caja cerrada");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("historial")]
    [Authorize(Policy = "Administrador")]
    public async Task<IActionResult> Historial([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null)
    {
        try
        {
            var result = await _cajaService.ObtenerHistorialAsync(page, pageSize, desde, hasta);

            // Con caja abierta los totales en BD siguen en cero hasta el cierre; el listado debe reflejar el preview en vivo.
            PreviewCierreCajaResponse? previewAbierto = null;
            try
            {
                if (result.Items.Any(c => c.Estado == "Abierto"))
                {
                    previewAbierto = await _cajaService.ObtenerPreviewCierreAsync();
                }
            }
            catch
            {
                // Sin sesión abierta o error transitorio: se devuelven valores persistidos.
            }

            return OkResponse(new Models.Api.PagedResult<object>
            {
                Items = result.Items.Select(c =>
                {
                    var usarPreview = previewAbierto != null
                        && c.Estado == "Abierto"
                        && c.Id == previewAbierto.CierreId;
                    return (object)new
                    {
                        c.Id,
                        c.FechaCierre,
                        c.FechaHoraCierre,
                        c.Estado,
                        c.MontoInicial,
                        TotalGeneral = usarPreview ? previewAbierto!.TotalGeneral : c.TotalGeneral,
                        TotalEfectivo = usarPreview ? previewAbierto!.TotalEfectivo : c.TotalEfectivo,
                        TotalTarjeta = usarPreview ? previewAbierto!.TotalTarjeta : c.TotalTarjeta,
                        TotalTransferencia = usarPreview ? previewAbierto!.TotalTransferencia : c.TotalTransferencia,
                        MontoEsperado = usarPreview ? previewAbierto!.MontoEsperado : c.MontoEsperado,
                        c.MontoReal,
                        c.Diferencia,
                        Usuario = c.Usuario != null ? c.Usuario.NombreCompleto : null
                    };
                }).ToList(),
                Page = result.Page,
                PageSize = result.PageSize,
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages
            });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("historial/excel")]
    [Authorize(Policy = "Administrador")]
    public async Task<IActionResult> HistorialExcel([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        try
        {
            var cierres = await _cajaService.ObtenerHistorialParaExportAsync(desde, hasta);
            var items = cierres.Select(c => new
            {
                id = c.Id,
                fecha = c.FechaHoraCierre,
                estado = c.Estado,
                montoInicial = c.MontoInicial,
                totalVentas = c.TotalGeneral,
                montoEsperado = c.MontoEsperado,
                montoReal = c.MontoReal,
                diferencia = c.Diferencia,
                usuario = c.Usuario != null ? c.Usuario.NombreCompleto : ""
            }).ToList();

            var excelBytes = _excelExportService.ExportarHistorialCierres(items);
            var nombreArchivo = $"historial_cierres_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("cierres/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public Task<IActionResult> DetalleCierrePorCierres(int id) => DetalleCierreCore(id);

    [HttpGet("historial/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public Task<IActionResult> DetalleCierrePorHistorial(int id) => DetalleCierreCore(id);

    [HttpGet("cierre/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public Task<IActionResult> DetalleCierre(int id) => DetalleCierreCore(id);

    private async Task<IActionResult> DetalleCierreCore(int id)
    {
        try
        {
            var cierre = await _cajaService.ObtenerCierrePorIdAsync(id);
            if (cierre == null) return FailResponse("Cierre no encontrado.", StatusCodes.Status404NotFound);
            var pagos = await _cajaService.ObtenerPagosPorFechaCierreAsync(cierre.FechaCierre);

            return OkResponse(new
            {
                Cierre = new
                {
                    cierre.Id,
                    cierre.FechaCierre,
                    cierre.FechaHoraCierre,
                    cierre.Estado,
                    cierre.MontoInicial,
                    cierre.TotalEfectivo,
                    cierre.TotalTarjeta,
                    cierre.TotalTransferencia,
                    cierre.TotalCordobas,
                    cierre.TotalDolares,
                    cierre.TotalGeneral,
                    cierre.TotalOrdenes,
                    cierre.TotalPagos,
                    cierre.MontoEsperado,
                    cierre.MontoReal,
                    cierre.Diferencia,
                    cierre.Observaciones,
                    Usuario = cierre.Usuario != null ? cierre.Usuario.NombreCompleto : null
                },
                Pagos = pagos
            });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}

public class AperturaCajaRequest
{
    public decimal MontoInicial { get; set; }
}

public class CierreCajaRequest
{
    public decimal? MontoReal { get; set; }
    public string? Observaciones { get; set; }
}
