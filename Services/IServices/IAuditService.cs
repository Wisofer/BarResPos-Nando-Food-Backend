using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface IAuditService
{
    Task RegistrarAccionAsync(string accion, string? mesaNumero, int? pedidoId, object? detalles, int? usuarioId = null);
    Task<List<RegistroAuditoria>> ObtenerLogsAsync(DateTime? desde, DateTime? hasta, string? accion, string? usuario, string? mesa, string? severidad, int limit = 100);
}
