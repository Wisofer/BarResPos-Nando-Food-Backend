using BarRestPOS.Data;
using BarRestPOS.Models.Api;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using QuestPDF.Infrastructure;
using System.Text;

// Inicializar QuestPDF antes de cualquier uso
QuestPDF.Settings.License = LicenseType.Community;

// Configurar Npgsql para manejar DateTime correctamente con PostgreSQL
// Esto permite usar DateTime.Now sin especificar UTC explícitamente
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "BarRestPOS API", Version = "v1" });
});

// Configurar URLs en minúsculas
builder.Services.AddRouting(options => options.LowercaseUrls = true);

// Configurar Entity Framework con SQLite en ruta persistente de AppData
string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BarRestPOS");
if (!Directory.Exists(appDataFolder))
{
    Directory.CreateDirectory(appDataFolder);
}
string persistentDbPath = Path.Combine(appDataFolder, "barrestpos.db");
string connectionString = $"Data Source={persistentDbPath}";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(connectionString);
    // Ignorar advertencia de cambios pendientes en el modelo (común en SQLite local de desarrollo al aplicar migraciones en el arranque)
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// Configurar CORS para frontend React (Vite/local y orígenes configurados)
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (corsOrigins == null || corsOrigins.Length == 0)
{
    corsOrigins = new[]
    {
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "https://localhost:5173",
        "https://127.0.0.1:5173"
    };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var jwtSecret = builder.Configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey no configurado.");
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "BarRestPOS";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "BarRestPOSClients";
var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

// Autenticación API: solo JWT (Bearer). Para HTML de impresión en iframe, opcional: ?access_token=...
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtKey,
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api/v1/impresion"))
                {
                    var token = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(token))
                        context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

// Configurar Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Administrador", policy => policy.RequireClaim("Rol", "Administrador"));
    options.AddPolicy("Normal", policy => policy.RequireClaim("Rol", "Normal", "Administrador"));
    options.AddPolicy("Caja", policy => policy.RequireClaim("Rol", "Caja", "Administrador"));
    options.AddPolicy("FacturasPagos", policy => policy.RequireClaim("Rol", "Normal", "Administrador"));
    options.AddPolicy("Pagos", policy => policy.RequireClaim("Rol", "Caja", "Normal", "Administrador"));
    options.AddPolicy("Inventario", policy => policy.RequireClaim("Rol", "Normal", "Administrador"));
    options.AddPolicy("Cocina", policy => policy.RequireClaim("Rol", "Cocinero", "Administrador"));
    options.AddPolicy("Cajero", policy => policy.RequireClaim("Rol", "Cajero", "Caja", "Administrador"));
});

// Registrar servicios
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IClienteService, ClienteService>(); // Necesario para FacturaService y PagoService
builder.Services.AddScoped<IServicioService, ServicioService>();
builder.Services.AddScoped<IFacturaService, FacturaService>();
builder.Services.AddScoped<IPagoService, PagoService>();
builder.Services.AddScoped<IPdfService>(sp => 
{
    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    var context = sp.GetRequiredService<ApplicationDbContext>();
    return new PdfService(environment, context);
});
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<IConfiguracionService, ConfiguracionService>();
builder.Services.AddScoped<IReporteService, ReporteService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.Configure<R2StorageOptions>(builder.Configuration.GetSection("R2Storage"));
builder.Services.AddScoped<IR2StorageService, R2StorageService>();

// Servicios del POS Restaurante
builder.Services.AddScoped<IMesaService, MesaService>();
builder.Services.AddScoped<IUbicacionService, UbicacionService>(); // Necesario para ubicaciones de mesas (Salón, Terraza, Bar, etc.)
builder.Services.AddScoped<ICajaService, CajaService>();

// Servicios de Productos del Restaurante
builder.Services.AddScoped<ICategoriaProductoService, CategoriaProductoService>();

// Servicios de Inventario
builder.Services.AddScoped<IInventarioService, InventarioService>();
builder.Services.AddScoped<OrdenLineasReemplazoService>();
builder.Services.AddScoped<IProveedorService, ProveedorService>();
builder.Services.AddScoped<PedidoCancelacionService>();

// Servicio de Impresión
builder.Services.AddScoped<IImpresionService, ImpresionService>();

// Servicio de Exportación a Excel
builder.Services.AddScoped<ExcelExportService>();

var app = builder.Build();

// Aplicar migraciones e inicializar datos
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
            try
            {
                // Asegurar la existencia e inyección de migraciones automáticas en la base de datos local SQLite de AppData
                logger.LogInformation("Aplicando migraciones automáticas en SQLite local en AppData...");
                dbContext.Database.Migrate();
                logger.LogInformation("Base de datos SQLite lista y actualizada.");

                // Realizar un respaldo automático rápido al iniciar el sistema
                BarRestPOS.Utils.BackupHelper.CrearRespaldo("inicio");

                // Crear usuario admin si no existe (PRIMERO, para poder hacer login)
                InicializarUsuarioAdmin.CrearAdminSiNoExiste(dbContext, logger);

                // Inicializar ubicaciones, mesas y categorías por defecto para la primera experiencia (demo o primer uso)
                InicializarDatosDemostracion.Inicializar(dbContext, logger);
                
                // COMENTADO: El administrador creará manualmente los servicios/productos
                // Inicializar servicios si no existen (opcional, puede fallar si la tabla no existe)
                // try
                // {
                //     if (!dbContext.Servicios.Any())
                //     {
                //         logger.LogInformation("Inicializando servicios en la base de datos...");
                //         
                //         var servicios = new List<Servicio>
                //         {
                //             new Servicio { Nombre = SD.ServiciosPrincipales.Servicio1, Precio = SD.ServiciosPrincipales.PrecioServicio1, Categoria = SD.CategoriaInternet, Activo = true, FechaCreacion = DateTime.UtcNow },
                //             new Servicio { Nombre = SD.ServiciosPrincipales.Servicio2, Precio = SD.ServiciosPrincipales.PrecioServicio2, Categoria = SD.CategoriaInternet, Activo = true, FechaCreacion = DateTime.UtcNow },
                //             new Servicio { Nombre = SD.ServiciosPrincipales.Servicio3, Precio = SD.ServiciosPrincipales.PrecioServicio3, Categoria = SD.CategoriaInternet, Activo = true, FechaCreacion = DateTime.UtcNow },
                //             new Servicio { Nombre = SD.ServiciosPrincipales.ServicioEspecial, Precio = SD.ServiciosPrincipales.PrecioServicioEspecial, Categoria = SD.CategoriaInternet, Activo = true, FechaCreacion = DateTime.UtcNow }
                //         };
                //         
                //         dbContext.Servicios.AddRange(servicios);
                //         dbContext.SaveChanges();
                //         logger.LogInformation("Servicios inicializados correctamente.");
                //     }
                // }
                // catch (Exception ex)
                // {
                //     logger.LogWarning(ex, "No se pudo inicializar servicios (la tabla puede no existir aún). Continuando...");
                // }

        // Migraciones de clientes eliminadas - Sistema solo para restaurante/bar

        // Crear plantilla por defecto de WhatsApp si no existe
        InicializarPlantillaWhatsApp.CrearPlantillaDefaultSiNoExiste(dbContext, logger);

        // Inicializar tipo de cambio por defecto si no existe
        var configuracionService = scope.ServiceProvider.GetRequiredService<IConfiguracionService>();
        configuracionService.CrearSiNoExiste(
            "TipoCambioDolar",
            SD.TipoCambioDolar.ToString("F2"),
            "Tipo de cambio dólar a córdoba (C$ por $1)"
        );
        configuracionService.CrearSiNoExiste(
            "Tickets:NombreRestaurante",
            "BarResPos",
            "Nombre comercial del restaurante/bar para los tickets impresos y digitales"
        );
        configuracionService.CrearSiNoExiste(
            SD.ConfigClavePinCancelacionPedidos,
            "0000",
            "PIN para autorizar cancelación de pedidos (cambiar en producción)"
        );
        configuracionService.CrearSiNoExiste(
            "Mesas:HabilitarVistaZonas",
            "true",
            "Habilitar la vista de zonas en mesas (true/false)"
        );
        configuracionService.CrearSiNoExiste(
            "Mesas:HabilitarVistaPlano",
            "true",
            "Habilitar la vista de plano físico en mesas (true/false)"
        );

        // El administrador creará manualmente las categorías y ubicaciones de inventario
        // (El archivo InicializarInventario.cs fue eliminado)

    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error al inicializar la base de datos");
    }
}

// Configurar el pipeline HTTP
// Forwarded Headers lo antes posible: X-Forwarded-Proto para esquema HTTPS detrás de proxy.
var useForwardedHeaders = app.Configuration.GetValue("ForwardedHeaders:Enabled", !app.Environment.IsDevelopment());
if (useForwardedHeaders)
{
    var fwd = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };
    app.UseForwardedHeaders(fwd);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionHandler = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(exceptionHandler?.Error, "Error interno del servidor en {Path}", exceptionHandler?.Path ?? context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ApiResponse<object>
            {
                Success = false,
                Message = "Error interno del servidor.",
                Data = null
            });
        });
    });
    app.UseHsts();
    // UseHttpsRedirection va DESPUÉS de UseCors: si va antes, el preflight OPTIONS recibe 302/307
    // y el navegador falla con "Redirect is not allowed for a preflight request".
}
else
{
    // En desarrollo, solo usar HTTP si no hay HTTPS configurado
    // Esto evita la advertencia de redirección HTTPS
    app.UseDeveloperExceptionPage();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "BarRestPOS API v1");
    });
}

app.UseStaticFiles();

// Mapear la carpeta de subidas persistente en AppData para que sirva archivos estáticos (Logo e imágenes de productos)
string persistentUploadsDir = Path.Combine(appDataFolder, "uploads");
if (!Directory.Exists(persistentUploadsDir))
{
    Directory.CreateDirectory(persistentUploadsDir);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(persistentUploadsDir),
    RequestPath = "/uploads"
});
app.UseRouting();

app.UseCors("FrontendCors");

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Servir la SPA (frontend) para tablets en la LAN
var frontendRoot = app.Environment.WebRootPath;
if (!string.IsNullOrEmpty(frontendRoot) && Directory.Exists(frontendRoot))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.Run();
