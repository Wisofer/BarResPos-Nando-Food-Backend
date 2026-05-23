using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/dashboard")]
public class DashboardApiController : BaseApiController
{
    private readonly IDashboardService _dashboardService;

    public DashboardApiController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int topProductos = 5)
    {
        if (topProductos < 1) topProductos = 5;
        if (topProductos > 20) topProductos = 20;
        try
        {
            var resumen = await _dashboardService.ObtenerResumenAsync(desde, hasta, topProductos);
            return OkResponse(resumen);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}
