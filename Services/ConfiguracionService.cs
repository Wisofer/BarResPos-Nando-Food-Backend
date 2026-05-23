using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BarRestPOS.Services;

public class ConfiguracionService : IConfiguracionService
{
    private readonly ApplicationDbContext _context;

    public ConfiguracionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public string? ObtenerValor(string clave)
    {
        var configuracion = _context.Configuraciones
            .FirstOrDefault(c => c.Clave == clave);
        return configuracion?.Valor;
    }

    public decimal? ObtenerValorDecimal(string clave)
    {
        var valor = ObtenerValor(clave);
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        
        if (decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var resultado))
            return resultado;
        
        return null;
    }

    public List<Configuracion> ObtenerTodas()
    {
        return _context.Configuraciones
            .OrderBy(c => c.Clave)
            .ToList();
    }

    public Configuracion? ObtenerPorClave(string clave)
    {
        return _context.Configuraciones
            .FirstOrDefault(c => c.Clave == clave);
    }

    public void ActualizarValor(string clave, string valor, string? usuarioActualizacion = null)
    {
        try
        {
            var configuracion = _context.Configuraciones
                .FirstOrDefault(c => c.Clave == clave);
            
            if (configuracion == null)
            {
                // Si no existe, crearla
                configuracion = new Configuracion
                {
                    Clave = clave,
                    Valor = valor,
                    Descripcion = clave == "TipoCambioDolar" ? "Tipo de cambio dólar a córdoba (C$ por $1)" : null,
                    FechaCreacion = DateTime.Now
                };
                _context.Configuraciones.Add(configuracion);
            }
            else
            {
                configuracion.Valor = valor;
                configuracion.FechaActualizacion = DateTime.Now;
                configuracion.UsuarioActualizacion = usuarioActualizacion;
            }
            
            _context.SaveChanges();
        }
        catch (Exception ex)
        {
            // Re-lanzar la excepción con más contexto
            throw new InvalidOperationException($"Error al actualizar la configuración '{clave}': {ex.Message}", ex);
        }
    }

    public void CrearSiNoExiste(string clave, string valor, string? descripcion = null)
    {
        var existe = _context.Configuraciones.Any(c => c.Clave == clave);
        if (!existe)
        {
            var configuracion = new Configuracion
            {
                Clave = clave,
                Valor = valor,
                Descripcion = descripcion,
                FechaCreacion = DateTime.Now
            };
            _context.Configuraciones.Add(configuracion);
            _context.SaveChanges();
        }
    }
}

