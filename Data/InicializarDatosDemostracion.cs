using System;
using System.Collections.Generic;
using System.Linq;
using BarRestPOS.Models.Entities;
using Microsoft.Extensions.Logging;

namespace BarRestPOS.Data;

public static class InicializarDatosDemostracion
{
    public static void Inicializar(ApplicationDbContext context, ILogger logger)
    {
        try
        {
            // Failsafe robusto: Verificar si los datos de demostración ya fueron inicializados alguna vez.
            // Si el cliente elimina todas sus mesas o categorías en el futuro, no queremos volver a creárselas al reiniciar la app.
            var yaInicializado = context.Configuraciones
                .Any(c => c.Clave == "Sistema:DatosDemostracionInicializados");

            if (yaInicializado)
            {
                logger.LogInformation("Los datos de demostración ya fueron inicializados en el pasado. Se omite el siembro.");
                return;
            }

            logger.LogInformation("=== INICIANDO SIEMBRO DE DATOS DE DEMOSTRACIÓN ===");

            // 1. Inicializar Ubicaciones si no existen
            if (!context.Ubicaciones.Any())
            {
                logger.LogInformation("Inicializando ubicaciones por defecto para Bar/Restaurante...");
                var ubicaciones = new List<Ubicacion>
                {
                    new Ubicacion { Nombre = "Salón Principal", Descripcion = "Área principal climatizada", Activo = true, FechaCreacion = DateTime.Now },
                    new Ubicacion { Nombre = "Terraza", Descripcion = "Área al aire libre con vista y ventilación", Activo = true, FechaCreacion = DateTime.Now },
                    new Ubicacion { Nombre = "Barra", Descripcion = "Barra de licores y bebidas rápidas", Activo = true, FechaCreacion = DateTime.Now }
                };

                context.Ubicaciones.AddRange(ubicaciones);
                context.SaveChanges();
                logger.LogInformation("Ubicaciones inicializadas correctamente.");
            }

            // 2. Inicializar Mesas si no existen
            if (!context.Mesas.Any())
            {
                logger.LogInformation("Inicializando mesas por defecto vinculadas a ubicaciones...");
                var salon = context.Ubicaciones.FirstOrDefault(u => u.Nombre == "Salón Principal");
                var terraza = context.Ubicaciones.FirstOrDefault(u => u.Nombre == "Terraza");
                var barra = context.Ubicaciones.FirstOrDefault(u => u.Nombre == "Barra");

                var mesas = new List<Mesa>();

                if (salon != null)
                {
                    mesas.Add(new Mesa { Numero = "Mesa 1", Capacidad = 4, Estado = "Libre", UbicacionId = salon.Id, Activo = true, FechaCreacion = DateTime.Now });
                    mesas.Add(new Mesa { Numero = "Mesa 2", Capacidad = 4, Estado = "Libre", UbicacionId = salon.Id, Activo = true, FechaCreacion = DateTime.Now });
                    mesas.Add(new Mesa { Numero = "Mesa 3", Capacidad = 6, Estado = "Libre", UbicacionId = salon.Id, Activo = true, FechaCreacion = DateTime.Now });
                    mesas.Add(new Mesa { Numero = "Mesa 4", Capacidad = 2, Estado = "Libre", UbicacionId = salon.Id, Activo = true, FechaCreacion = DateTime.Now });
                }

                if (terraza != null)
                {
                    mesas.Add(new Mesa { Numero = "Terraza 1", Capacidad = 4, Estado = "Libre", UbicacionId = terraza.Id, Activo = true, FechaCreacion = DateTime.Now });
                    mesas.Add(new Mesa { Numero = "Terraza 2", Capacidad = 4, Estado = "Libre", UbicacionId = terraza.Id, Activo = true, FechaCreacion = DateTime.Now });
                    mesas.Add(new Mesa { Numero = "Terraza 3", Capacidad = 6, Estado = "Libre", UbicacionId = terraza.Id, Activo = true, FechaCreacion = DateTime.Now });
                }

                if (barra != null)
                {
                    mesas.Add(new Mesa { Numero = "Barra 1", Capacidad = 1, Estado = "Libre", UbicacionId = barra.Id, Activo = true, FechaCreacion = DateTime.Now });
                    mesas.Add(new Mesa { Numero = "Barra 2", Capacidad = 1, Estado = "Libre", UbicacionId = barra.Id, Activo = true, FechaCreacion = DateTime.Now });
                }

                if (mesas.Any())
                {
                    context.Mesas.AddRange(mesas);
                    context.SaveChanges();
                    logger.LogInformation("Mesas inicializadas correctamente.");
                }
            }

            // 3. Inicializar Categorías de Producto si no existen
            if (!context.CategoriasProducto.Any())
            {
                logger.LogInformation("Inicializando categorías de producto por defecto para Bar/Restaurante...");
                var categorias = new List<CategoriaProducto>
                {
                    new CategoriaProducto 
                    { 
                        Nombre = "Entradas", 
                        Descripcion = "Aperitivos, boquitas y entradas ligeras", 
                        ColorHex = "#eab308", 
                        IconoNombre = "UtensilsCrossed", 
                        Orden = 1, 
                        RequiereCocina = true, 
                        Activo = true, 
                        FechaCreacion = DateTime.Now 
                    },
                    new CategoriaProducto 
                    { 
                        Nombre = "Platos Fuertes", 
                        Descripcion = "Platos fuertes, cortes de carne y especialidades", 
                        ColorHex = "#ef4444", 
                        IconoNombre = "ChefHat", 
                        Orden = 2, 
                        RequiereCocina = true, 
                        Activo = true, 
                        FechaCreacion = DateTime.Now 
                    },
                    new CategoriaProducto 
                    { 
                        Nombre = "Bebidas Frías", 
                        Descripcion = "Jugos naturales, gaseosas y tés helados", 
                        ColorHex = "#3b82f6", 
                        IconoNombre = "GlassWater", 
                        Orden = 3, 
                        RequiereCocina = false, 
                        Activo = true, 
                        FechaCreacion = DateTime.Now 
                    },
                    new CategoriaProducto 
                    { 
                        Nombre = "Bebidas Calientes", 
                        Descripcion = "Café espresso, capuchino y tés calientes", 
                        ColorHex = "#f97316", 
                        IconoNombre = "Coffee", 
                        Orden = 4, 
                        RequiereCocina = false, 
                        Activo = true, 
                        FechaCreacion = DateTime.Now 
                    },
                    new CategoriaProducto 
                    { 
                        Nombre = "Cócteles", 
                        Descripcion = "Margaritas, mojitos y coctelería de autor", 
                        ColorHex = "#a855f7", 
                        IconoNombre = "Wine", 
                        Orden = 5, 
                        RequiereCocina = false, 
                        Activo = true, 
                        FechaCreacion = DateTime.Now 
                    },
                    new CategoriaProducto 
                    { 
                        Nombre = "Postres", 
                        Descripcion = "Tres leches, flan de la casa y repostería", 
                        ColorHex = "#ec4899", 
                        IconoNombre = "Cake", 
                        Orden = 6, 
                        RequiereCocina = false, 
                        Activo = true, 
                        FechaCreacion = DateTime.Now 
                    }
                };

                context.CategoriasProducto.AddRange(categorias);
                context.SaveChanges();
                logger.LogInformation("Categorías de producto inicializadas correctamente.");
            }

            // Guardar bandera persistente en la tabla de configuraciones
            context.Configuraciones.Add(new Configuracion
            {
                Clave = "Sistema:DatosDemostracionInicializados",
                Valor = "true",
                Descripcion = "Indica si los datos por defecto ya se crearon al menos una vez.",
                UsuarioActualizacion = "Sistema"
            });
            context.SaveChanges();
            logger.LogInformation("Bandera de inicialización guardada exitosamente. No se volverá a ejecutar en futuros arranques.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al inicializar datos de demostración en la base de datos.");
            throw;
        }
    }
}
