using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BarRestPOS.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize(Policy = "Administrador")]
[Route("api/v1/auditoria")]
public class AuditoriaApiController : BaseApiController
{
    private readonly IAuditService _auditService;

    public AuditoriaApiController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerLogs(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] string? accion,
        [FromQuery] string? usuario,
        [FromQuery] string? mesa,
        [FromQuery] string? severidad,
        [FromQuery] int limit = 100)
    {
        try
        {
            if (limit < 1) limit = 100;
            if (limit > 500) limit = 500;

            var logs = await _auditService.ObtenerLogsAsync(desde, hasta, accion, usuario, mesa, severidad, limit);
            return OkResponse(logs);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}
