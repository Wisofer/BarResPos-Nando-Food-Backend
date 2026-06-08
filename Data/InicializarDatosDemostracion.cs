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
                var ubicaciones = new List<Ubicacion>
                {
                    new Ubicacion { Nombre = "Sala", Descripcion = "Área principal", Activo = true, FechaCreacion = DateTime.Now },
                    new Ubicacion { Nombre = "Barra", Descripcion = "Asientos en barra", Activo = true, FechaCreacion = DateTime.Now }
                };
                context.Ubicaciones.AddRange(ubicaciones);
                context.SaveChanges();
            }

            // 2. Inicializar Mesas si no existen
            if (!context.Mesas.Any())
            {
                var sala = context.Ubicaciones.FirstOrDefault(u => u.Nombre == "Sala");
                var barra = context.Ubicaciones.FirstOrDefault(u => u.Nombre == "Barra");

                var mesas = new List<Mesa>();

                if (sala != null)
                {
                    for(int i = 1; i <= 10; i++)
                    {
                        mesas.Add(new Mesa { Numero = $"Sala {i}", Capacidad = 4, Estado = "Libre", UbicacionId = sala.Id, Activo = true, FechaCreacion = DateTime.Now });
                    }
                }

                if (barra != null)
                {
                    for(int i = 1; i <= 10; i++)
                    {
                        mesas.Add(new Mesa { Numero = $"Barra {i}", Capacidad = 1, Estado = "Libre", UbicacionId = barra.Id, Activo = true, FechaCreacion = DateTime.Now });
                    }
                }

                if (mesas.Any())
                {
                    context.Mesas.AddRange(mesas);
                    context.SaveChanges();
                }
            }

            // 3. Inicializar Categorías de Producto si no existen
            if (!context.CategoriasProducto.Any())
            {
                var categorias = new List<CategoriaProducto>
                {
                    new CategoriaProducto { Nombre = "Hamburguesas", Descripcion = "Hamburguesas de Res y Pollo", ColorHex = "#ef4444", IconoNombre = "UtensilsCrossed", Orden = 1, RequiereCocina = true, Activo = true, FechaCreacion = DateTime.Now },
                    new CategoriaProducto { Nombre = "Alitas", Descripcion = "Alitas con salsas variadas", ColorHex = "#f97316", IconoNombre = "Flame", Orden = 2, RequiereCocina = true, Activo = true, FechaCreacion = DateTime.Now },
                    new CategoriaProducto { Nombre = "Tacos", Descripcion = "Tacos al Pastor y Birria", ColorHex = "#eab308", IconoNombre = "UtensilsCrossed", Orden = 3, RequiereCocina = true, Activo = true, FechaCreacion = DateTime.Now },
                    new CategoriaProducto { Nombre = "Quesadillas", Descripcion = "Quesadillas y Quesabirria", ColorHex = "#facc15", IconoNombre = "UtensilsCrossed", Orden = 4, RequiereCocina = true, Activo = true, FechaCreacion = DateTime.Now },
                    new CategoriaProducto { Nombre = "Subway", Descripcion = "Subway de Res y Pollo", ColorHex = "#22c55e", IconoNombre = "UtensilsCrossed", Orden = 5, RequiereCocina = true, Activo = true, FechaCreacion = DateTime.Now },
                    new CategoriaProducto { Nombre = "Nachos", Descripcion = "Nachos de Pollo, Res o Mixto", ColorHex = "#eab308", IconoNombre = "UtensilsCrossed", Orden = 6, RequiereCocina = true, Activo = true, FechaCreacion = DateTime.Now },
                    new CategoriaProducto { Nombre = "De Todo Un Poco", Descripcion = "Salchipapas, Deditos, Burritos", ColorHex = "#a855f7", IconoNombre = "ChefHat", Orden = 7, RequiereCocina = true, Activo = true, FechaCreacion = DateTime.Now },
                    new CategoriaProducto { Nombre = "Extras", Descripcion = "Papas y Queso", ColorHex = "#94a3b8", IconoNombre = "UtensilsCrossed", Orden = 8, RequiereCocina = true, Activo = true, FechaCreacion = DateTime.Now },
                    new CategoriaProducto { Nombre = "Bebidas", Descripcion = "Batidos y refrescos", ColorHex = "#3b82f6", IconoNombre = "GlassWater", Orden = 9, RequiereCocina = false, Activo = true, FechaCreacion = DateTime.Now }
                };

                context.CategoriasProducto.AddRange(categorias);
                context.SaveChanges();
            }

            // 4. Inicializar Productos (Servicios) si no existen
            if (!context.Servicios.Any())
            {
                var cats = context.CategoriasProducto.ToList();
                var catHamburguesas = cats.FirstOrDefault(c => c.Nombre == "Hamburguesas");
                var catAlitas = cats.FirstOrDefault(c => c.Nombre == "Alitas");
                var catTacos = cats.FirstOrDefault(c => c.Nombre == "Tacos");
                var catQuesadillas = cats.FirstOrDefault(c => c.Nombre == "Quesadillas");
                var catSubway = cats.FirstOrDefault(c => c.Nombre == "Subway");
                var catNachos = cats.FirstOrDefault(c => c.Nombre == "Nachos");
                var catDeTodo = cats.FirstOrDefault(c => c.Nombre == "De Todo Un Poco");
                var catExtras = cats.FirstOrDefault(c => c.Nombre == "Extras");
                var catBebidas = cats.FirstOrDefault(c => c.Nombre == "Bebidas");

                var productos = new List<Servicio>
                {
                    // Hamburguesas
                    new Servicio { Codigo = "HMB01", Nombre = "Hamburguesa de Res", Precio = 200, Categoria = "Comidas", CategoriaProductoId = catHamburguesas?.Id, EsPreparado = true },
                    new Servicio { Codigo = "HMB02", Nombre = "Hamburguesa de Pollo", Precio = 190, Categoria = "Comidas", CategoriaProductoId = catHamburguesas?.Id, EsPreparado = true },
                    new Servicio { Codigo = "HMB03", Nombre = "Nando Hamburguesa", Precio = 260, Categoria = "Comidas", CategoriaProductoId = catHamburguesas?.Id, EsPreparado = true },

                    // Alitas (Base)
                    new Servicio { Codigo = "ALI01", Nombre = "Alitas", Precio = 250, Categoria = "Comidas", CategoriaProductoId = catAlitas?.Id, EsPreparado = true },

                    // Tacos
                    new Servicio { Codigo = "TAC01", Nombre = "Taco al Pastor", Precio = 170, Categoria = "Comidas", CategoriaProductoId = catTacos?.Id, EsPreparado = true },
                    new Servicio { Codigo = "TAC02", Nombre = "Taco de Birria", Precio = 230, Categoria = "Comidas", CategoriaProductoId = catTacos?.Id, EsPreparado = true },

                    // Quesadillas
                    new Servicio { Codigo = "QUE01", Nombre = "Quesadilla", Precio = 140, Categoria = "Comidas", CategoriaProductoId = catQuesadillas?.Id, EsPreparado = true },
                    new Servicio { Codigo = "QUE02", Nombre = "Quesabirria", Precio = 220, Categoria = "Comidas", CategoriaProductoId = catQuesadillas?.Id, EsPreparado = true },

                    // Subway
                    new Servicio { Codigo = "SUB01", Nombre = "Subway de Res", Precio = 200, Categoria = "Comidas", CategoriaProductoId = catSubway?.Id, EsPreparado = true },
                    new Servicio { Codigo = "SUB02", Nombre = "Subway de Pollo", Precio = 170, Categoria = "Comidas", CategoriaProductoId = catSubway?.Id, EsPreparado = true },

                    // Nachos
                    new Servicio { Codigo = "NAC01", Nombre = "Nachos de Pollo", Precio = 220, Categoria = "Comidas", CategoriaProductoId = catNachos?.Id, EsPreparado = true },
                    new Servicio { Codigo = "NAC02", Nombre = "Nachos de Res", Precio = 220, Categoria = "Comidas", CategoriaProductoId = catNachos?.Id, EsPreparado = true },
                    new Servicio { Codigo = "NAC03", Nombre = "Nachos Mixto", Precio = 240, Categoria = "Comidas", CategoriaProductoId = catNachos?.Id, EsPreparado = true },

                    // De todo un poco
                    new Servicio { Codigo = "VAR01", Nombre = "Salchipapa de Pollo o Res", Precio = 190, Categoria = "Comidas", CategoriaProductoId = catDeTodo?.Id, EsPreparado = true },
                    new Servicio { Codigo = "VAR02", Nombre = "Deditos de Pollo", Precio = 180, Categoria = "Comidas", CategoriaProductoId = catDeTodo?.Id, EsPreparado = true },
                    new Servicio { Codigo = "VAR03", Nombre = "Palomitas de Pollo", Precio = 150, Categoria = "Comidas", CategoriaProductoId = catDeTodo?.Id, EsPreparado = true },
                    new Servicio { Codigo = "VAR04", Nombre = "Burritos", Precio = 180, Categoria = "Comidas", CategoriaProductoId = catDeTodo?.Id, EsPreparado = true },

                    // Extras
                    new Servicio { Codigo = "EXT01", Nombre = "Papas", Precio = 70, Categoria = "Comidas", CategoriaProductoId = catExtras?.Id, EsPreparado = true },
                    new Servicio { Codigo = "EXT02", Nombre = "Papas Gajo", Precio = 90, Categoria = "Comidas", CategoriaProductoId = catExtras?.Id, EsPreparado = true },
                    new Servicio { Codigo = "EXT03", Nombre = "Queso Cheddar", Precio = 50, Categoria = "Comidas", CategoriaProductoId = catExtras?.Id, EsPreparado = true },

                    // Bebidas
                    new Servicio { Codigo = "BEB01", Nombre = "Batidos", Precio = 100, Categoria = "Bebidas", CategoriaProductoId = catBebidas?.Id, EsPreparado = false }
                };

                context.Servicios.AddRange(productos);
                context.SaveChanges();
            }

            // 5. Inicializar Modificadores para Alitas
            if (!context.ProductoOpcionGrupos.Any())
            {
                var alitas = context.Servicios.FirstOrDefault(s => s.Nombre == "Alitas");
                if (alitas != null)
                {
                    var grupoSalsas = new ProductoOpcionGrupo
                    {
                        Nombre = "Elige tu Salsa",
                        Obligatorio = true,
                        MinSeleccion = 1,
                        MaxSeleccion = 1,
                        Activo = true,
                        ServicioId = alitas.Id
                    };
                    context.ProductoOpcionGrupos.Add(grupoSalsas);
                    context.SaveChanges();

                    var opcionesSalsas = new List<ProductoOpcionItem>
                    {
                        new ProductoOpcionItem { Nombre = "Salsa BBQ", PrecioAdicional = 0, GrupoId = grupoSalsas.Id, Activo = true },
                        new ProductoOpcionItem { Nombre = "Salsa Búfalo", PrecioAdicional = 0, GrupoId = grupoSalsas.Id, Activo = true },
                        new ProductoOpcionItem { Nombre = "Salsa Ranch", PrecioAdicional = 0, GrupoId = grupoSalsas.Id, Activo = true }
                    };
                    context.ProductoOpcionItems.AddRange(opcionesSalsas);
                    context.SaveChanges();
                }
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
