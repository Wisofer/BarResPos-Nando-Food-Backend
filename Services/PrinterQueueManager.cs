using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace BarRestPOS.Services;

/// <summary>
/// Gestiona semáforos exclusivos por impresora física para evitar la colisión
/// de escrituras concurrentes en la cola del Spooler de Windows.
/// </summary>
public class PrinterQueueManager
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    /// <summary>
    /// Ejecuta de forma estrictamente secuencial la acción de impresión asociada a la impresora especificada.
    /// </summary>
    public async Task<bool> RunSerializedAsync(string printerName, Func<bool> printAction)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return printAction();

        var normalizedPrinter = printerName.Trim().ToLowerInvariant();
        var semaphore = _semaphores.GetOrAdd(normalizedPrinter, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        try
        {
            return printAction();
        }
        finally
        {
            semaphore.Release();
        }
    }
}
