using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;
using Npgsql;

namespace TempDbCleaner;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("==================================================================");
        Console.WriteLine("    BARRESTPOS - INICIALIZADOR Y SIMULADOR BATCH DE BASE DE DATOS ");
        Console.WriteLine("==================================================================");

        var connectionString = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("ERROR: No se encontró ConnectionStrings:DefaultConnection en appsettings.json.");
            return 1;
        }

        Console.WriteLine("· Conexión resuelta correctamente.");
        
        await using var conn = new NpgsqlConnection(connectionString);
        try
        {
            await conn.OpenAsync();
            Console.WriteLine("· Conexión abierta con PostgreSQL (Neon).");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: No se pudo conectar a la base de datos: {ex.Message}");
            return 1;
        }

        // ==========================================
        // FASE 1: LIMPIEZA TOTAL OPERATIVA
        // ==========================================
        Console.WriteLine("\n[FASE 1] Limpiando tablas operativas...");
        await using (var tx = await conn.BeginTransactionAsync())
        {
            try
            {
                var tablesToClean = new (string Label, string Sql)[]
                {
                    ("PagoFacturas", "DELETE FROM \"PagoFacturas\";"),
                    ("Pagos", "DELETE FROM \"Pagos\";"),
                    ("OrdenLineaOpciones", "DELETE FROM \"OrdenLineaOpciones\";"),
                    ("OrdenProductos (Líneas de pedido)", "DELETE FROM \"OrdenProductos\";"),
                    ("Ordenes (Facturas/Pedidos)", "DELETE FROM \"Ordenes\";"),
                    ("ClienteProductos", "DELETE FROM \"ClienteProductos\";"),
                    ("RefreshTokens", "DELETE FROM \"RefreshTokens\";"),
                    ("MovimientosInventario", "DELETE FROM \"MovimientosInventario\";"),
                    ("CierresCaja", "DELETE FROM \"CierresCaja\";"),
                    ("Clientes", "DELETE FROM \"Clientes\";"),
                    ("Proveedores", "DELETE FROM \"Proveedores\";"),
                    ("PlantillasMensajeWhatsApp", "DELETE FROM \"PlantillasMensajeWhatsApp\";"),
                    ("Mesas", "DELETE FROM \"Mesas\";"),
                    ("Ubicaciones", "DELETE FROM \"Ubicaciones\";"),
                    ("ProductoOpcionItems", "DELETE FROM \"ProductoOpcionItems\";"),
                    ("ProductoOpcionGrupos", "DELETE FROM \"ProductoOpcionGrupos\";")
                };

                foreach (var (label, sql) in tablesToClean)
                {
                    await using var cmd = new NpgsqlCommand(sql, conn, tx);
                    int affected = await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"  · {label}: {affected} filas eliminadas.");
                }

                await tx.CommitAsync();
                Console.WriteLine("✔ Base de datos operativa limpiada con éxito.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Console.Error.WriteLine($"ERROR en Fase 1 (Limpieza): {ex.Message}");
                return 1;
            }
        }

        // ==========================================
        // FASE 2: CREACIÓN DE UBICACIONES Y MESAS (53 MESAS)
        // ==========================================
        Console.WriteLine("\n[FASE 2] Creando ubicaciones y 53 mesas...");
        var locations = new Dictionary<string, int>(); // Nombre -> Id
        var mesasCreadas = 0;

        await using (var tx = await conn.BeginTransactionAsync())
        {
            try
            {
                var locsToCreate = new[]
                {
                    ("Salón", "Área principal climatizada"),
                    ("Terraza", "Área al aire libre con vista al jardín"),
                    ("VIP", "Salón exclusivo privado"),
                    ("Bar", "Barra principal y mesas altas")
                };

                foreach (var (nombre, desc) in locsToCreate)
                {
                    await using var cmd = new NpgsqlCommand(
                        "INSERT INTO \"Ubicaciones\" (\"Nombre\", \"Descripcion\", \"Activo\", \"FechaCreacion\") VALUES (@n, @d, true, CURRENT_TIMESTAMP) RETURNING \"Id\";",
                        conn, tx);
                    cmd.Parameters.AddWithValue("n", nombre);
                    cmd.Parameters.AddWithValue("d", desc);
                    int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    locations[nombre] = id;
                    Console.WriteLine($"  · Ubicación creada: {nombre} (Id={id})");
                }

                // Generar mesas para Salón: Mesas 1 a 20 (Capacidades 2, 4, 6)
                for (int i = 1; i <= 20; i++)
                {
                    int cap = i <= 6 ? 2 : (i <= 16 ? 4 : 6);
                    await InsertMesaAsync(conn, tx, $"Mesa {i}", cap, locations["Salón"]);
                    mesasCreadas++;
                }

                // Generar mesas para Terraza: Mesas 21 a 35 (Capacidad 4)
                for (int i = 21; i <= 35; i++)
                {
                    await InsertMesaAsync(conn, tx, $"Mesa {i}", 4, locations["Terraza"]);
                    mesasCreadas++;
                }

                // Generar mesas para VIP: Mesas VIP 1 a 8 (Capacidad 6, 8)
                for (int i = 1; i <= 8; i++)
                {
                    int cap = i <= 4 ? 6 : 8;
                    await InsertMesaAsync(conn, tx, $"Mesa VIP {i}", cap, locations["VIP"]);
                    mesasCreadas++;
                }

                // Generar mesas para Bar: Barra 1 a 10 (Capacidad 1, 2)
                for (int i = 1; i <= 10; i++)
                {
                    int cap = i <= 6 ? 1 : 2;
                    await InsertMesaAsync(conn, tx, $"Barra {i}", cap, locations["Bar"]);
                    mesasCreadas++;
                }

                await tx.CommitAsync();
                Console.WriteLine($"✔ Se crearon 4 ubicaciones y {mesasCreadas} mesas correctamente.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Console.Error.WriteLine($"ERROR en Fase 2 (Mesas): {ex.Message}");
                return 1;
            }
        }

        // ==========================================
        // FASE 3: VERIFICACIÓN Y ENRIQUECIMIENTO DE MENÚ DE PRODUCTOS
        // ==========================================
        Console.WriteLine("\n[FASE 3] Configurando categorías, productos y opciones personalizadas...");
        var categories = new Dictionary<string, int>(); // Nombre -> Id
        var products = new List<DbProduct>();

        await using (var tx = await conn.BeginTransactionAsync())
        {
            try
            {
                // Asegurar categorías básicas
                var catsToCreate = new (string Nombre, string Desc, string Color, string Icono, int Orden, bool Cocina)[]
                {
                    ("Entradas", "Entradas y aperitivos", "#1ABC9C", "bowl-food", 1, true),
                    ("Comidas", "Platillos fuertes de cocina", "#E74C3C", "utensils", 2, true),
                    ("Bebidas", "Bebidas no alcohólicas y refrescos", "#3498DB", "glass-water", 3, false),
                    ("Licores", "Cervezas, cócteles y tragos", "#9B59B6", "wine-glass", 4, false),
                    ("Postres", "Postres dulces y pasteles", "#E67E22", "ice-cream", 5, false)
                };

                foreach (var cat in catsToCreate)
                {
                    // Comprobar si existe la categoría por nombre
                    await using var cmdCheck = new NpgsqlCommand(
                        "SELECT \"Id\" FROM \"CategoriasProducto\" WHERE lower(\"Nombre\") = lower(@n) LIMIT 1;", conn, tx);
                    cmdCheck.Parameters.AddWithValue("n", cat.Nombre);
                    var existingId = await cmdCheck.ExecuteScalarAsync();

                    int catId;
                    if (existingId != null)
                    {
                        catId = Convert.ToInt32(existingId);
                    }
                    else
                    {
                        await using var cmdIns = new NpgsqlCommand(
                            @"INSERT INTO ""CategoriasProducto"" (""Nombre"", ""Descripcion"", ""ColorHex"", ""IconoNombre"", ""Orden"", ""RequiereCocina"", ""Activo"", ""FechaCreacion"")
                              VALUES (@n, @d, @c, @i, @o, @k, true, CURRENT_TIMESTAMP) RETURNING ""Id"";", conn, tx);
                        cmdIns.Parameters.AddWithValue("n", cat.Nombre);
                        cmdIns.Parameters.AddWithValue("d", cat.Desc);
                        cmdIns.Parameters.AddWithValue("c", cat.Color);
                        cmdIns.Parameters.AddWithValue("i", cat.Icono);
                        cmdIns.Parameters.AddWithValue("o", cat.Orden);
                        cmdIns.Parameters.AddWithValue("k", cat.Cocina);
                        catId = Convert.ToInt32(await cmdIns.ExecuteScalarAsync());
                        Console.WriteLine($"  · Categoría creada: {cat.Nombre} (Id={catId})");
                    }
                    categories[cat.Nombre] = catId;
                }

                // Asegurar productos
                var defaultProducts = new (string Codigo, string Nombre, string Desc, decimal Precio, decimal PrecioCompra, string Cat, bool StockCtrl, bool Prep)[]
                {
                    ("ENT001", "Papas Fritas Suprema", "Papas fritas crujientes bañadas en queso cheddar fundido y trocitos de tocino ahumado.", 120m, 45m, "Entradas", true, true),
                    ("ENT002", "Alitas BBQ (6 uds)", "Alitas de pollo glaseadas en salsa barbacoa de la casa, acompañadas de apio y aderezo ranch.", 180m, 70m, "Entradas", true, true),
                    ("ENT003", "Tequeños de Queso (5 uds)", "Deditos de masa crujiente rellenos de queso blanco derretido, con aderezo tártara.", 130m, 40m, "Entradas", true, true),
                    
                    ("COM001", "Hamburguesa Premium", "Carne angus de 200g a la parrilla, queso cheddar, tocino crujiente, cebolla caramelizada, lechuga y tomate en pan brioche.", 220m, 95m, "Comidas", true, true),
                    ("COM002", "Corte de Entraña 300g", "Corte premium de entraña a la parrilla con chimichurri casero y acompañamiento a elegir.", 480m, 210m, "Comidas", true, true),
                    ("COM003", "Fettuccine Alfredo con Pollo", "Pasta fettuccine en salsa alfredo cremosa a base de mantequilla y parmesano, con pechuga de pollo grillada.", 250m, 105m, "Comidas", true, true),
                    ("COM004", "Tacos de Pollo (3 uds)", "Tacos de tortilla de maíz con pollo desmenuzado sazonado, pico de gallo, guacamole y crema ácida.", 150m, 60m, "Comidas", true, true),
                    ("COM005", "Club Sandwich", "Sandwich de tres pisos con jamón, queso, pollo a la plancha, tocino, huevo, lechuga, tomate y mayonesa. Con papas fritas.", 170m, 70m, "Comidas", true, true),
                    
                    ("BEB001", "Coca-Cola 12oz", "Gaseosa embotellada de 354ml.", 35m, 15m, "Bebidas", true, false),
                    ("BEB002", "Jugo de Calala Natural", "Refrescante jugo natural de maracuyá de 16oz.", 45m, 12m, "Bebidas", true, true),
                    ("BEB003", "Limonada Imperial", "Limonada frappé de 16oz con hierbabuena.", 50m, 15m, "Bebidas", true, true),
                    
                    ("LIC001", "Cerveza Toña", "Cerveza nacional tipo lager de 12oz.", 60m, 32m, "Licores", true, false),
                    ("LIC002", "Cerveza Victoria", "Cerveza nacional clásica de 12oz.", 60m, 32m, "Licores", true, false),
                    ("LIC003", "Mojito Clásico", "Ron blanco, zumo de limón fresco, azúcar, hierbabuena fresca y agua mineral frappé.", 120m, 40m, "Licores", true, true),
                    ("LIC004", "Flor de Caña 7 Años Extra Seco", "Servicio de ron Flor de Caña con Coca-Cola o agua mineral.", 140m, 50m, "Licores", true, true),
                    
                    ("POS001", "Torta Tres Leches", "Delicioso pastel húmedo empapado en tres tipos de leche, decorado con merengue y canela.", 90m, 30m, "Postres", true, true),
                    ("POS002", "Brownie con Helado", "Brownie de chocolate tibio acompañado de una bola de helado de vainilla y fudge de chocolate.", 110m, 40m, "Postres", true, true)
                };

                foreach (var p in defaultProducts)
                {
                    // Comprobar si existe el producto por código
                    await using var cmdCheck = new NpgsqlCommand(
                        "SELECT \"Id\", \"Nombre\", \"Precio\", \"PrecioCompra\", \"Categoria\", \"ControlarStock\" FROM \"Productos\" WHERE lower(\"Codigo\") = lower(@c) LIMIT 1;", conn, tx);
                    cmdCheck.Parameters.AddWithValue("c", p.Codigo);
                    
                    int prodId;
                    decimal currentPrice = p.Precio;
                    decimal currentCost = p.PrecioCompra;
                    string currentCat = p.Cat;
                    bool currentStockCtrl = p.StockCtrl;
                    string currentName = p.Nombre;

                    using (var reader = await cmdCheck.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            prodId = reader.GetInt32(0);
                            currentName = reader.GetString(1);
                            currentPrice = reader.GetDecimal(2);
                            currentCost = reader.GetDecimal(3);
                            currentCat = reader.GetString(4);
                            currentStockCtrl = reader.GetBoolean(5);
                            
                            // Ya existe, lo guardamos
                            products.Add(new DbProduct(prodId, p.Codigo, currentName, currentPrice, currentCost, currentCat, currentStockCtrl, p.Prep));
                            continue;
                        }
                    }

                    // Si no existe, lo insertamos
                    await using var cmdIns = new NpgsqlCommand(
                        @"INSERT INTO ""Productos"" (""Codigo"", ""Nombre"", ""Descripcion"", ""Precio"", ""PrecioCompra"", ""Categoria"", ""CategoriaProductoId"", ""Stock"", ""StockMinimo"", ""ControlarStock"", ""EsPreparado"", ""ImagenUrl"", ""Destacado"", ""Activo"", ""FechaCreacion"")
                          VALUES (@c, @n, @d, @p, @pc, @cat, @catId, 100, 10, @sc, @ep, '', true, true, CURRENT_TIMESTAMP) RETURNING ""Id"";", conn, tx);
                    cmdIns.Parameters.AddWithValue("c", p.Codigo);
                    cmdIns.Parameters.AddWithValue("n", p.Nombre);
                    cmdIns.Parameters.AddWithValue("d", p.Desc);
                    cmdIns.Parameters.AddWithValue("p", p.Precio);
                    cmdIns.Parameters.AddWithValue("pc", p.PrecioCompra);
                    cmdIns.Parameters.AddWithValue("cat", p.Cat);
                    cmdIns.Parameters.AddWithValue("catId", categories[p.Cat]);
                    cmdIns.Parameters.AddWithValue("sc", p.StockCtrl);
                    cmdIns.Parameters.AddWithValue("ep", p.Prep);

                    prodId = Convert.ToInt32(await cmdIns.ExecuteScalarAsync());
                    Console.WriteLine($"  · Producto creado: {p.Nombre} (Id={prodId}, Código={p.Codigo})");
                    products.Add(new DbProduct(prodId, p.Codigo, p.Nombre, p.Precio, p.PrecioCompra, p.Cat, p.StockCtrl, p.Prep));
                }

                // ==========================================
                // AGREGAR OPCIONES ESPECIALES EN COMIDAS
                // ==========================================
                Console.WriteLine("  · Agregando opciones especiales de preparación a los platillos fuertes...");

                // 1. Opciones para Hamburguesa Premium (COM001)
                var burger = products.First(x => x.Codigo == "COM001");
                int burgerTerminoGrupo = await InsertOpcionGrupoAsync(conn, tx, burger.Id, "Término de la Carne", 1, true, 1, 1);
                await InsertOpcionItemAsync(conn, tx, burgerTerminoGrupo, "Término Medio", 1, 0m);
                await InsertOpcionItemAsync(conn, tx, burgerTerminoGrupo, "Tres Cuartos", 2, 0m);
                await InsertOpcionItemAsync(conn, tx, burgerTerminoGrupo, "Bien Cocida", 3, 0m);

                int burgerAdicionalesGrupo = await InsertOpcionGrupoAsync(conn, tx, burger.Id, "Ingredientes Adicionales", 2, false, 0, 3);
                await InsertOpcionItemAsync(conn, tx, burgerAdicionalesGrupo, "Queso Cheddar Extra", 1, 20m);
                await InsertOpcionItemAsync(conn, tx, burgerAdicionalesGrupo, "Tocino Ahumado Crujiente", 2, 30m);
                await InsertOpcionItemAsync(conn, tx, burgerAdicionalesGrupo, "Doble Torta de Carne (Angus 200g)", 3, 80m);

                // 2. Opciones para Corte de Entraña 300g (COM002)
                var estrana = products.First(x => x.Codigo == "COM002");
                int estranaTerminoGrupo = await InsertOpcionGrupoAsync(conn, tx, estrana.Id, "Término de Cocción", 1, true, 1, 1);
                await InsertOpcionItemAsync(conn, tx, estranaTerminoGrupo, "Término Medio (Jugoso)", 1, 0m);
                await InsertOpcionItemAsync(conn, tx, estranaTerminoGrupo, "Tres Cuartos (Recomendado)", 2, 0m);
                await InsertOpcionItemAsync(conn, tx, estranaTerminoGrupo, "Bien Cocida", 3, 0m);

                int estranaAcompGrupo = await InsertOpcionGrupoAsync(conn, tx, estrana.Id, "Guarnición Acompañante", 2, true, 1, 2);
                await InsertOpcionItemAsync(conn, tx, estranaAcompGrupo, "Papas Fritas al Romero", 1, 0m);
                await InsertOpcionItemAsync(conn, tx, estranaAcompGrupo, "Ensalada Verde de la Casa", 2, 0m);
                await InsertOpcionItemAsync(conn, tx, estranaAcompGrupo, "Puré de Papa Cremoso", 3, 20m);
                await InsertOpcionItemAsync(conn, tx, estranaAcompGrupo, "Vegetales al Grill", 4, 15m);

                // 3. Opciones para Fettuccine Alfredo con Pollo (COM003)
                var pasta = products.First(x => x.Codigo == "COM003");
                int pastaExtrasGrupo = await InsertOpcionGrupoAsync(conn, tx, pasta.Id, "Ingredientes Extras", 1, false, 0, 2);
                await InsertOpcionItemAsync(conn, tx, pastaExtrasGrupo, "Champiñones Salteados", 1, 25m);
                await InsertOpcionItemAsync(conn, tx, pastaExtrasGrupo, "Extra Queso Parmesano Rallado", 2, 15m);
                await InsertOpcionItemAsync(conn, tx, pastaExtrasGrupo, "Tiras de Tocino Crujiente", 3, 30m);

                await tx.CommitAsync();
                Console.WriteLine("✔ Categorías, productos y grupos de opciones creados e indexados.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Console.Error.WriteLine($"ERROR en Fase 3 (Menú y Opciones): {ex.Message}");
                return 1;
            }
        }

        // ==========================================
        // FASE 4: CREACIÓN DE USUARIOS OPERATIVOS Y PROVEEDOR
        // ==========================================
        Console.WriteLine("\n[FASE 4] Creando usuarios y proveedor...");
        var users = new Dictionary<string, int>(); // NombreUsuario -> Id
        int proveedorId;

        await using (var tx = await conn.BeginTransactionAsync())
        {
            try
            {
                // Crear Proveedor
                await using (var cmdProv = new NpgsqlCommand(
                    @"INSERT INTO ""Proveedores"" (""Nombre"", ""Telefono"", ""Email"", ""Direccion"", ""Contacto"", ""Observaciones"", ""Activo"", ""FechaCreacion"")
                      VALUES ('Distribuidora La Única', '+505 2222-7777', 'ventas@launica.com.ni', 'Pista Juan Pablo II, Managua', 'Lic. Francisco Dávila', 'Distribuidor oficial de abarrotes, carnes y bebidas.', true, CURRENT_TIMESTAMP)
                      ON CONFLICT (""Nombre"") DO UPDATE SET ""Telefono"" = EXCLUDED.""Telefono""
                      RETURNING ""Id"";", conn, tx))
                {
                    proveedorId = Convert.ToInt32(await cmdProv.ExecuteScalarAsync());
                    Console.WriteLine($"  · Proveedor creado/actualizado: Distribuidora La Única (Id={proveedorId})");
                }

                // Crear usuarios con contraseñas hasheadas en SHA256 base64
                var usersToCreate = new (string Username, string FullName, string Role)[]
                {
                    ("admin", "Administrador del Sistema", "Administrador"),
                    ("carlos", "Carlos Cajero López", "Cajero"),
                    ("juan", "Juan Mesero Solís", "Mesero"),
                    ("maria", "María Mesera Duarte", "Mesero"),
                    ("pedro", "Pedro Mesero Martínez", "Mesero"),
                    ("luis", "Luis Bartender Castro", "Bartender"),
                    ("chef", "Chef Ejecutivo Mario", "Cocinero")
                };

                foreach (var u in usersToCreate)
                {
                    var hashedPass = HashPassword(u.Username);
                    await using var cmdIns = new NpgsqlCommand(
                        @"INSERT INTO ""Usuarios"" (""NombreUsuario"", ""Contrasena"", ""NombreCompleto"", ""Rol"", ""Activo"")
                          VALUES (@u, @p, @fn, @r, true)
                          ON CONFLICT (""NombreUsuario"")
                          DO UPDATE SET ""Contrasena"" = EXCLUDED.""Contrasena"", ""NombreCompleto"" = EXCLUDED.""NombreCompleto"", ""Rol"" = EXCLUDED.""Rol""
                          RETURNING ""Id"";", conn, tx);
                    cmdIns.Parameters.AddWithValue("u", u.Username);
                    cmdIns.Parameters.AddWithValue("p", hashedPass);
                    cmdIns.Parameters.AddWithValue("fn", u.FullName);
                    cmdIns.Parameters.AddWithValue("r", u.Role);

                    int userId = Convert.ToInt32(await cmdIns.ExecuteScalarAsync());
                    users[u.Username] = userId;
                    Console.WriteLine($"  · Usuario creado: {u.Username} [{u.Role}] (Id={userId})");
                }

                // Crear plantilla por defecto de WhatsApp
                var msgText = "Hola {NombreCliente},\n\nLe enviamos su factura del mes {Mes}.\n\n🔗 Descargar PDF:\n{EnlacePDF}\n\nGracias por su preferencia.";
                await using (var cmdWp = new NpgsqlCommand(
                    @"INSERT INTO ""PlantillasMensajeWhatsApp"" (""Nombre"", ""Mensaje"", ""Activa"", ""EsDefault"", ""FechaCreacion"")
                      VALUES ('Plantilla por Defecto', @msg, true, true, CURRENT_TIMESTAMP);", conn, tx))
                {
                    cmdWp.Parameters.AddWithValue("msg", msgText);
                    await cmdWp.ExecuteNonQueryAsync();
                    Console.WriteLine("  · Plantilla de WhatsApp por defecto creada con éxito.");
                }

                await tx.CommitAsync();
                Console.WriteLine("✔ Usuarios operativos y proveedor creados con éxito.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Console.Error.WriteLine($"ERROR en Fase 4 (Usuarios/Proveedores): {ex.Message}");
                return 1;
            }
        }

        // Cargar las mesas e indexar por ubicación
        var tablesList = new List<DbMesa>();
        await using (var cmd = new NpgsqlCommand("SELECT \"Id\", \"Numero\", \"UbicacionId\" FROM \"Mesas\" WHERE \"Activo\" = true;", conn))
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tablesList.Add(new DbMesa(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
            }
        }

        // Cargar las opciones de productos para simular selecciones de comida
        var productOptionsMap = new Dictionary<int, List<DbOptionGroup>>();
        await using (var cmd = new NpgsqlCommand(
            @"SELECT g.""Id"" AS GroupId, g.""ServicioId"", g.""Nombre"" AS GroupName, g.""Obligatorio"", 
                     i.""Id"" AS ItemId, i.""Nombre"" AS ItemName, i.""PrecioAdicional""
              FROM ""ProductoOpcionGrupos"" g
              JOIN ""ProductoOpcionItems"" i ON g.""Id"" = i.""GrupoId""
              WHERE g.""Activo"" = true AND i.""Activo"" = true;", conn))
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int sId = reader.GetInt32(1);
                int gId = reader.GetInt32(0);
                string gName = reader.GetString(2);
                bool oblig = reader.GetBoolean(3);
                int iId = reader.GetInt32(4);
                string iName = reader.GetString(5);
                decimal price = reader.GetDecimal(6);

                if (!productOptionsMap.ContainsKey(sId))
                    productOptionsMap[sId] = new List<DbOptionGroup>();

                var groups = productOptionsMap[sId];
                var group = groups.FirstOrDefault(x => x.Id == gId);
                if (group == null)
                {
                    group = new DbOptionGroup(gId, gName, oblig);
                    groups.Add(group);
                }
                group.Items.Add(new DbOptionItem(iId, iName, price));
            }
        }

        // ==========================================
        // FASE 5: ALGORITMO DE SIMULACIÓN DE 2 MESES (60 DÍAS)
        // ==========================================
        Console.WriteLine("\n[FASE 5] Iniciando simulación batch operativa de 2 meses...");
        
        var random = new Random(42); // Seed para reproducibilidad
        var today = DateTime.Today;
        var startDate = today.AddDays(-60);
        
        var meserosUsernames = new[] { "juan", "maria", "pedro" };
        var globalOrderSequence = 10001;

        // Bucle día por día
        for (var day = startDate; day < today; day = day.AddDays(1))
        {
            var isWeekend = day.DayOfWeek == DayOfWeek.Friday || 
                            day.DayOfWeek == DayOfWeek.Saturday || 
                            day.DayOfWeek == DayOfWeek.Sunday;

            int numOrders = isWeekend ? random.Next(40, 86) : random.Next(15, 31);
            
            // Trackers diarios para el Cierre de Caja
            var dailyEfectivo = 0m;
            var dailyTarjeta = 0m;
            var dailyTransferencia = 0m;
            var dailyCordobas = 0m;
            var dailyDolares = 0m;
            var dailyOrderCount = 0;
            var dailyPagoCount = 0;

            // Construir un único script PL/pgSQL block para todo el día!
            var sb = new StringBuilder();
            sb.AppendLine("DO $$");
            sb.AppendLine("DECLARE");
            sb.AppendLine("    v_order_id int;");
            sb.AppendLine("    v_line_id int;");
            sb.AppendLine("    v_pago_id int;");
            sb.AppendLine("    v_stock_ant int;");
            sb.AppendLine("BEGIN");

            // 1. Reabastecimiento semanal de inventario (cada domingo)
            if (day.DayOfWeek == DayOfWeek.Sunday)
            {
                foreach (var p in products.Where(x => x.ControlarStock))
                {
                    int cantReabast = random.Next(60, 101); // Comprar entre 60 y 100 unidades
                    var reabastTime = day.Date.AddHours(9).AddMinutes(random.Next(0, 60));
                    var factNumber = $"FAC-{day:yyyyMMdd}-{random.Next(100, 999)}";

                    sb.AppendLine($@"
                        SELECT ""Stock"" INTO v_stock_ant FROM ""Productos"" WHERE ""Id"" = {p.Id};
                        UPDATE ""Productos"" SET ""Stock"" = v_stock_ant + {cantReabast} WHERE ""Id"" = {p.Id};
                        INSERT INTO ""MovimientosInventario"" (""ProductoId"", ""Tipo"", ""Subtipo"", ""Cantidad"", ""CostoUnitario"", ""CostoTotal"", ""Fecha"", ""UsuarioId"", ""ProveedorId"", ""NumeroFactura"", ""FacturaId"", ""Observaciones"", ""StockAnterior"", ""StockNuevo"")
                        VALUES ({p.Id}, 'Entrada', 'Compra', {cantReabast}, {p.PrecioCompra.ToString(CultureInfo.InvariantCulture)}, {(p.PrecioCompra * cantReabast).ToString(CultureInfo.InvariantCulture)}, '{reabastTime:yyyy-MM-dd HH:mm:ss}', {users["admin"]}, {proveedorId}, '{factNumber}', NULL, 'Reabastecimiento de stock planificado semanal.', v_stock_ant, v_stock_ant + {cantReabast});
                    ");
                }
            }

            // 2. Generar órdenes del día
            for (int ordIdx = 0; ordIdx < numOrders; ordIdx++)
            {
                var orderNum = $"ORD-{globalOrderSequence++}";
                
                // Determinar hora de la orden
                DateTime orderTime;
                var hourPct = random.Next(0, 100);
                if (hourPct < 60) // 60% en la noche/cena (6:30 PM - 10:30 PM)
                {
                    orderTime = day.Date.AddHours(18).AddMinutes(30).AddMinutes(random.Next(0, 240));
                }
                else if (hourPct < 90) // 30% en el almuerzo (12:00 PM - 2:30 PM)
                {
                    orderTime = day.Date.AddHours(12).AddMinutes(random.Next(0, 150));
                }
                else // 10% media tarde (3:00 PM - 6:00 PM)
                {
                    orderTime = day.Date.AddHours(15).AddMinutes(random.Next(0, 180));
                }

                // Determinar origen del pedido
                var originPct = random.Next(0, 100);
                string origin = "Salon";
                int? mesaId = null;
                string? delivNombre = null;
                string? delivTel = null;
                string? delivDir = null;

                if (originPct < 75) // 75% Salón
                {
                    origin = "Salon";
                    var m = tablesList[random.Next(0, tablesList.Count)];
                    mesaId = m.Id;
                }
                else if (originPct < 90) // 15% Delivery
                {
                    origin = "Delivery";
                    var customer = GetRandomCustomer(random);
                    delivNombre = customer.Nombre;
                    delivTel = customer.Tel;
                    delivDir = customer.Dir;
                }
                else // 10% Llevar
                {
                    origin = "Llevar";
                }

                // Mesero
                var meseroUser = meserosUsernames[random.Next(0, meserosUsernames.Length)];
                int meseroId = users[meseroUser];

                // Estado de la orden
                var isCanceled = random.Next(0, 100) < 2; // 1.5% cancelados
                string estadoOrden = isCanceled ? "Cancelado" : "Pagado";
                string estadoCocina = isCanceled ? "Pendiente" : "Entregado";

                // Productos del pedido (1 a 5)
                int numItems = random.Next(1, 6);
                var orderProducts = new List<OrderProductSim>();
                var firstProductId = products[0].Id;
                
                var selectedProducts = new List<DbProduct>();
                for (int k = 0; k < numItems; k++)
                {
                    var prod = products[random.Next(0, products.Count)];
                    if (!selectedProducts.Any(x => x.Id == prod.Id))
                        selectedProducts.Add(prod);
                }

                if (selectedProducts.Count > 0)
                    firstProductId = selectedProducts[0].Id;

                var orderTotal = 0m;

                foreach (var prod in selectedProducts)
                {
                    int cant = random.Next(1, 3); // 1 o 2 unidades
                    decimal subtotal = prod.Precio * cant;
                    var notesBuilder = new List<string>();

                    var lineOptions = new List<OrderLineOptionSim>();

                    // Procesar opciones especiales si tiene configuradas
                    if (productOptionsMap.ContainsKey(prod.Id))
                    {
                        var groups = productOptionsMap[prod.Id];
                        foreach (var grp in groups)
                        {
                            if (grp.Obligatorio || random.Next(0, 100) < 40)
                            {
                                var optItem = grp.Items[random.Next(0, grp.Items.Count)];
                                decimal extraPrice = optItem.PrecioAdicional * cant;
                                subtotal += extraPrice;

                                lineOptions.Add(new OrderLineOptionSim(grp.Id, optItem.Id, grp.Name, optItem.Name, optItem.PrecioAdicional));
                                
                                if (optItem.PrecioAdicional > 0m)
                                    notesBuilder.Add($"{optItem.Name} (+C${optItem.PrecioAdicional:N0})");
                                else
                                    notesBuilder.Add(optItem.Name);
                            }
                        }
                    }

                    string? notaStr = notesBuilder.Count > 0 ? string.Join(", ", notesBuilder) : null;
                    orderProducts.Add(new OrderProductSim(prod, cant, prod.Precio, subtotal, notaStr, lineOptions));
                    orderTotal += subtotal;
                }

                var mesaIdStr = mesaId.HasValue ? mesaId.ToString() : "NULL";
                var delivNombreStr = delivNombre != null ? $"'{delivNombre.Replace("'", "''")}'" : "NULL";
                var delivTelStr = delivTel != null ? $"'{delivTel.Replace("'", "''")}'" : "NULL";
                var delivDirStr = delivDir != null ? $"'{delivDir.Replace("'", "''")}'" : "NULL";
                var fechaPagadoStr = isCanceled ? "NULL" : $"'{orderTime.AddMinutes(random.Next(30, 75)):yyyy-MM-dd HH:mm:ss}'";
                var tiempoPrep = random.Next(12, 28);

                // SQL insert para la Orden
                sb.AppendLine($@"
                    INSERT INTO ""Ordenes"" (""Numero"", ""MesaId"", ""ClienteId"", ""MeseroId"", ""ServicioId"", ""Categoria"", ""OrigenPedido"", ""DeliveryClienteNombre"", ""DeliveryClienteTelefono"", ""DeliveryClienteDireccion"", ""Monto"", ""Estado"", ""EstadoCocina"", ""FechaCreacion"", ""FechaActualizacion"", ""FechaEnvioCocina"", ""FechaListo"", ""FechaServido"", ""FechaPagado"", ""TiempoPreparacion"", ""MesFacturacion"", ""ArchivoPDF"", ""Observaciones"")
                    VALUES ('{orderNum}', {mesaIdStr}, NULL, {meseroId}, {firstProductId}, 'General', '{origin}', {delivNombreStr}, {delivTelStr}, {delivDirStr}, {orderTotal.ToString(CultureInfo.InvariantCulture)}, '{estadoOrden}', '{estadoCocina}', '{orderTime:yyyy-MM-dd HH:mm:ss}', '{orderTime:yyyy-MM-dd HH:mm:ss}', '{orderTime.AddMinutes(2):yyyy-MM-dd HH:mm:ss}', '{orderTime.AddMinutes(15):yyyy-MM-dd HH:mm:ss}', '{orderTime.AddMinutes(18):yyyy-MM-dd HH:mm:ss}', {fechaPagadoStr}, {tiempoPrep}, '{orderTime:yyyy-MM-01 00:00:00}', '', NULL) 
                    RETURNING ""Id"" INTO v_order_id;
                ");

                // SQL insert para líneas de pedido (OrdenProductos) y sus opciones
                foreach (var op in orderProducts)
                {
                    var notasStr = op.Notas != null ? $"'{op.Notas.Replace("'", "''")}'" : "NULL";
                    
                    sb.AppendLine($@"
                        INSERT INTO ""OrdenProductos"" (""FacturaId"", ""ServicioId"", ""Cantidad"", ""PrecioUnitario"", ""Monto"", ""Notas"", ""Estado"")
                        VALUES (v_order_id, {op.Product.Id}, {op.Cantidad}, {op.PriceUnit.ToString(CultureInfo.InvariantCulture)}, {op.Monto.ToString(CultureInfo.InvariantCulture)}, {notasStr}, 'Entregado')
                        RETURNING ""Id"" INTO v_line_id;
                    ");

                    foreach (var opt in op.Options)
                    {
                        sb.AppendLine($@"
                            INSERT INTO ""OrdenLineaOpciones"" (""FacturaServicioId"", ""ProductoOpcionGrupoId"", ""ProductoOpcionItemId"", ""NombreGrupo"", ""NombreOpcion"", ""PrecioAdicional"")
                            VALUES (v_line_id, {opt.GroupId}, {opt.ItemId}, '{opt.GroupName.Replace("'", "''")}', '{opt.ItemName.Replace("'", "''")}', {opt.PrecioAdicional.ToString(CultureInfo.InvariantCulture)});
                        ");
                    }

                    // Actualizar stock e inventario si aplica (solo si no está cancelado)
                    if (!isCanceled && op.Product.ControlarStock)
                    {
                        sb.AppendLine($@"
                            SELECT ""Stock"" INTO v_stock_ant FROM ""Productos"" WHERE ""Id"" = {op.Product.Id};
                            UPDATE ""Productos"" SET ""Stock"" = v_stock_ant - {op.Cantidad} WHERE ""Id"" = {op.Product.Id};
                            INSERT INTO ""MovimientosInventario"" (""ProductoId"", ""Tipo"", ""Subtipo"", ""Cantidad"", ""CostoUnitario"", ""CostoTotal"", ""Fecha"", ""UsuarioId"", ""ProveedorId"", ""NumeroFactura"", ""FacturaId"", ""Observaciones"", ""StockAnterior"", ""StockNuevo"")
                            VALUES ({op.Product.Id}, 'Salida', 'Venta', -{op.Cantidad}, {op.Product.PrecioCompra.ToString(CultureInfo.InvariantCulture)}, {(op.Product.PrecioCompra * op.Cantidad).ToString(CultureInfo.InvariantCulture)}, '{orderTime:yyyy-MM-dd HH:mm:ss}', {meseroId}, NULL, NULL, v_order_id, 'Salida automatizada por venta.', v_stock_ant, v_stock_ant - {op.Cantidad});
                        ");
                    }
                }

                // 3. Registrar el Pago (si no está cancelado)
                if (!isCanceled)
                {
                    dailyOrderCount++;
                    dailyPagoCount++;

                    // Decidir método de pago
                    var payPct = random.Next(0, 100);
                    string typePayment = "Fisico";
                    string? banco = null;
                    string? tipoCuenta = null;

                    if (payPct < 50)
                    {
                        typePayment = "Fisico";
                    }
                    else if (payPct < 90)
                    {
                        typePayment = "Electronico";
                        banco = random.Next(0, 100) < 50 ? "BAC" : "Banpro";
                        tipoCuenta = random.Next(0, 100) < 70 ? "Cuenta C$" : "Billetera movil";
                    }
                    else
                    {
                        typePayment = "Mixto";
                    }

                    // Decidir moneda (80% Córdobas, 20% Dólares)
                    var isDolar = random.Next(0, 100) < 20;
                    string currency = isDolar ? "$" : "C$";
                    decimal tc = 36.80m;

                    decimal paymentMonto = orderTotal;
                    decimal? recFisico = null;
                    decimal? vuelFisico = null;
                    decimal? cordFis = null;
                    decimal? dolFis = null;
                    decimal? cordElec = null;
                    decimal? dolElec = null;

                    if (typePayment == "Fisico")
                    {
                        if (!isDolar) // Córdobas
                        {
                            cordFis = paymentMonto;
                            recFisico = Math.Ceiling(paymentMonto / 100m) * 100m;
                            if (recFisico < paymentMonto) recFisico = paymentMonto;
                            vuelFisico = recFisico - paymentMonto;
                            
                            dailyEfectivo += paymentMonto;
                            dailyCordobas += paymentMonto;
                        }
                        else // Dólares
                        {
                            decimal montoUsd = Math.Round(paymentMonto / tc, 2);
                            dolFis = montoUsd;
                            recFisico = Math.Ceiling(montoUsd / 10m) * 10m;
                            if (recFisico < montoUsd) recFisico = montoUsd;
                            vuelFisico = recFisico - montoUsd;

                            dailyEfectivo += paymentMonto;
                            dailyDolares += montoUsd;
                        }
                    }
                    else if (typePayment == "Electronico")
                    {
                        var isTransfer = random.Next(0, 100) < 30; // 30% transferencia, 70% tarjeta
                        decimal pMonto = paymentMonto;

                        if (!isDolar)
                        {
                            cordElec = pMonto;
                            dailyCordobas += pMonto;
                        }
                        else
                        {
                            decimal montoUsd = Math.Round(pMonto / tc, 2);
                            dolElec = montoUsd;
                            dailyDolares += montoUsd;
                        }

                        if (isTransfer)
                            dailyTransferencia += pMonto;
                        else
                            dailyTarjeta += pMonto;
                    }
                    else // Mixto
                    {
                        decimal medioMonto = Math.Round(paymentMonto / 2m, 2);
                        cordFis = medioMonto;
                        recFisico = medioMonto;
                        vuelFisico = 0m;
                        cordElec = paymentMonto - medioMonto;

                        dailyEfectivo += medioMonto;
                        dailyTarjeta += (paymentMonto - medioMonto);
                        dailyCordobas += paymentMonto;
                    }

                    var bancoStr = banco != null ? $"'{banco}'" : "NULL";
                    var tipoCuentaStr = tipoCuenta != null ? $"'{tipoCuenta}'" : "NULL";
                    var cordFisStr = cordFis.HasValue ? cordFis.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
                    var dolFisStr = dolFis.HasValue ? dolFis.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
                    var recFisicoStr = recFisico.HasValue ? recFisico.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
                    var vuelFisicoStr = vuelFisico.HasValue ? vuelFisico.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
                    var cordElecStr = cordElec.HasValue ? cordElec.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
                    var dolElecStr = dolElec.HasValue ? dolElec.Value.ToString(CultureInfo.InvariantCulture) : "NULL";

                    var pagoTime = orderTime.AddMinutes(random.Next(30, 60));

                    sb.AppendLine($@"
                        INSERT INTO ""Pagos"" (""FacturaId"", ""Monto"", ""Moneda"", ""TipoPago"", ""Banco"", ""TipoCuenta"", ""MontoRecibido"", ""Vuelto"", ""TipoCambio"", ""MontoCordobasFisico"", ""MontoDolaresFisico"", ""MontoRecibidoFisico"", ""VueltoFisico"", ""MontoCordobasElectronico"", ""MontoDolaresElectronico"", ""FechaPago"", ""Observaciones"", ""DescuentoMonto"", ""DescuentoMotivo"")
                        VALUES (v_order_id, {paymentMonto.ToString(CultureInfo.InvariantCulture)}, '{currency}', '{typePayment}', {bancoStr}, {tipoCuentaStr}, NULL, NULL, {tc.ToString(CultureInfo.InvariantCulture)}, {cordFisStr}, {dolFisStr}, {recFisicoStr}, {vuelFisicoStr}, {cordElecStr}, {dolElecStr}, '{pagoTime:yyyy-MM-dd HH:mm:ss}', NULL, 0, NULL)
                        RETURNING ""Id"" INTO v_pago_id;

                        INSERT INTO ""PagoFacturas"" (""PagoId"", ""FacturaId"", ""MontoAplicado"")
                        VALUES (v_pago_id, v_order_id, {paymentMonto.ToString(CultureInfo.InvariantCulture)});
                    ");
                }
            }

            // 4. Crear Cierre de Caja del Día (a las 11:30 PM)
            var fechaCierreDate = day.Date;
            var horaCierre = day.Date.AddHours(23).AddMinutes(30);
            
            decimal inicial = 2000m;
            decimal esperado = inicial + dailyEfectivo;
            
            decimal dif = 0m;
            int difPct = random.Next(0, 100);
            if (difPct < 15) dif = -10m;
            else if (difPct < 25) dif = 5m;
            else if (difPct < 30) dif = -20m;
            
            decimal realCaja = esperado + dif;
            decimal totalG = dailyEfectivo + dailyTarjeta + dailyTransferencia;

            sb.AppendLine($@"
                INSERT INTO ""CierresCaja"" (""FechaCierre"", ""FechaHoraCierre"", ""UsuarioId"", ""MontoInicial"", ""TotalEfectivo"", ""TotalTarjeta"", ""TotalTransferencia"", ""TotalCordobas"", ""TotalDolares"", ""TotalGeneral"", ""TotalOrdenes"", ""TotalPagos"", ""MontoEsperado"", ""MontoReal"", ""Diferencia"", ""Observaciones"", ""Estado"")
                VALUES ('{fechaCierreDate:yyyy-MM-dd}', '{horaCierre:yyyy-MM-dd HH:mm:ss}', {users["carlos"]}, {inicial.ToString(CultureInfo.InvariantCulture)}, {dailyEfectivo.ToString(CultureInfo.InvariantCulture)}, {dailyTarjeta.ToString(CultureInfo.InvariantCulture)}, {dailyTransferencia.ToString(CultureInfo.InvariantCulture)}, {dailyCordobas.ToString(CultureInfo.InvariantCulture)}, {dailyDolares.ToString(CultureInfo.InvariantCulture)}, {totalG.ToString(CultureInfo.InvariantCulture)}, {dailyOrderCount}, {dailyPagoCount}, {esperado.ToString(CultureInfo.InvariantCulture)}, {realCaja.ToString(CultureInfo.InvariantCulture)}, {dif.ToString(CultureInfo.InvariantCulture)}, 'Cierre automático operativo en simulación.', 'Cerrado');
            ");

            sb.AppendLine("END $$;");

            // Ejecutar el bloque PL/pgSQL diario en una única petición de base de datos!
            await using (var cmdCC = new NpgsqlCommand(sb.ToString(), conn))
            {
                await cmdCC.ExecuteNonQueryAsync();
            }

            // Imprimir progreso por terminal cada pocos días simulados
            if (day.Day % 5 == 0 || day == startDate || day == today.AddDays(-1))
            {
                Console.WriteLine($"  · [{day:yyyy-MM-dd}] Cierre de caja - Órdenes: {dailyOrderCount}, Total: C$ {totalG:N2}, Arqueo: {dif:+C$#;-C$#;C$0}");
            }
        }

        Console.WriteLine("\n✔ Simulación de 2 meses completada exitosamente.");
        Console.WriteLine("==================================================================");
        Console.WriteLine("    BASE DE DATOS RESTABLECIDA Y SIMULADA CORRECTAMENTE          ");
        Console.WriteLine("==================================================================");
        return 0;
    }

    // ==========================================
    // MÉTODOS DE APOYO
    // ==========================================

    static string? ResolveConnectionString()
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        var candidates = new[]
        {
            Path.Combine(repoRoot, "appsettings.Development.json"),
            Path.Combine(repoRoot, "appsettings.json")
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (!doc.RootElement.TryGetProperty("ConnectionStrings", out var connStrings)) continue;
                if (!connStrings.TryGetProperty("DefaultConnection", out var el)) continue;
                var cs = el.GetString();
                if (!string.IsNullOrWhiteSpace(cs))
                    return cs;
            }
            catch
            {
                // Siguiente archivo
            }
        }
        return null;
    }

    static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    static async Task InsertMesaAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string numero, int capacidad, int ubicacionId)
    {
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO \"Mesas\" (\"Numero\", \"Capacidad\", \"Estado\", \"UbicacionId\", \"Activo\", \"FechaCreacion\") VALUES (@num, @cap, 'Libre', @uId, true, CURRENT_TIMESTAMP);",
            conn, tx);
        cmd.Parameters.AddWithValue("num", numero);
        cmd.Parameters.AddWithValue("cap", capacidad);
        cmd.Parameters.AddWithValue("uId", ubicacionId);
        await cmd.ExecuteNonQueryAsync();
    }

    static async Task<int> InsertOpcionGrupoAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int servicioId, string nombre, int orden, bool obligatorio, int min, int max)
    {
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO ""ProductoOpcionGrupos"" (""ServicioId"", ""Nombre"", ""Orden"", ""Obligatorio"", ""MinSeleccion"", ""MaxSeleccion"", ""Activo"")
              VALUES (@sId, @nom, @ord, @oblig, @min, @max, true) RETURNING ""Id"";", conn, tx);
        cmd.Parameters.AddWithValue("sId", servicioId);
        cmd.Parameters.AddWithValue("nom", nombre);
        cmd.Parameters.AddWithValue("ord", orden);
        cmd.Parameters.AddWithValue("oblig", obligatorio);
        cmd.Parameters.AddWithValue("min", min);
        cmd.Parameters.AddWithValue("max", max);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    static async Task InsertOpcionItemAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int grupoId, string nombre, int orden, decimal precioAdicional)
    {
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO ""ProductoOpcionItems"" (""GrupoId"", ""Nombre"", ""Orden"", ""PrecioAdicional"", ""Activo"")
              VALUES (@gId, @nom, @ord, @price, true);", conn, tx);
        cmd.Parameters.AddWithValue("gId", grupoId);
        cmd.Parameters.AddWithValue("nom", nombre);
        cmd.Parameters.AddWithValue("ord", orden);
        cmd.Parameters.AddWithValue("price", precioAdicional);
        await cmd.ExecuteNonQueryAsync();
    }

    // Datos ficticios para Delivery
    static readonly (string Nombre, string Tel, string Dir)[] CustomersPool = new[]
    {
        ("William Solís", "+505 8888-1234", "Altamira d'este, de La Vicky 2c al lago."),
        ("María Estela Pérez", "+505 7777-5678", "Los Robles, del Hotel Seminole 1c al oeste."),
        ("Carlos Alberto Mendoza", "+505 8456-7890", "Bello Horizonte, de los semáforos 3c abajo."),
        ("Gabriela Mercedes López", "+505 8989-1122", "Reparto San Juan, del gimnasio Hércules 1c al sur."),
        ("Francisco Javier Ruiz", "+505 7654-3210", "Plaza Inter, 3c al sur, portón verde."),
        ("Elena Sofía Duarte", "+505 8123-4567", "Carretera Masaya km 8.5, Condominio Portal."),
        ("Roberto José Espinoza", "+505 7890-1234", "Pista Suburbana, frente a gasolinera Puma."),
        ("Tatiana Vanessa Rostrán", "+505 8345-6789", "Villa Fontana, del club Terraza 1/2c arriba.")
    };

    static (string Nombre, string Tel, string Dir) GetRandomCustomer(Random r)
    {
        return CustomersPool[r.Next(0, CustomersPool.Length)];
    }
}

// Clases Auxiliares de Soporte para simulación
public record DbProduct(int Id, string Codigo, string Nombre, decimal Precio, decimal PrecioCompra, string Categoria, bool ControlarStock, bool EsPreparado);
public record DbMesa(int Id, string Numero, int UbicacionId);
public record DbOptionItem(int Id, string Name, decimal PrecioAdicional);

public class DbOptionGroup
{
    public int Id { get; }
    public string Name { get; }
    public bool Obligatorio { get; }
    public List<DbOptionItem> Items { get; } = new();

    public DbOptionGroup(int id, string name, bool obligatorio)
    {
        Id = id;
        Name = name;
        Obligatorio = obligatorio;
    }
}

public record OrderProductSim(DbProduct Product, int Cantidad, decimal PriceUnit, decimal Monto, string? Notas, List<OrderLineOptionSim> Options);
public record OrderLineOptionSim(int GroupId, int ItemId, string GroupName, string ItemName, decimal PrecioAdicional);
