using Microsoft.EntityFrameworkCore;
using BarRestPOS.Models.Entities;

namespace BarRestPOS.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Entidades principales del POS
    public DbSet<Mesa> Mesas { get; set; }
    public DbSet<CategoriaProducto> CategoriasProducto { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Servicio> Servicios { get; set; } // Productos (mantiene nombre por compatibilidad)
    public DbSet<Factura> Facturas { get; set; } // Órdenes (mantiene nombre por compatibilidad)
    public DbSet<FacturaServicio> FacturaServicios { get; set; } // OrdenProductos (mantiene nombre por compatibilidad)
    public DbSet<ProductoOpcionGrupo> ProductoOpcionGrupos { get; set; }
    public DbSet<ProductoOpcionItem> ProductoOpcionItems { get; set; }
    public DbSet<FacturaServicioOpcionSeleccion> FacturaServicioOpcionesSeleccion { get; set; }
    public DbSet<Pago> Pagos { get; set; }
    public DbSet<PagoFactura> PagoFacturas { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<ClienteServicio> ClienteServicios { get; set; }
    public DbSet<PlantillaMensajeWhatsApp> PlantillasMensajeWhatsApp { get; set; }
    public DbSet<Configuracion> Configuraciones { get; set; }
    public DbSet<Ubicacion> Ubicaciones { get; set; }
    public DbSet<CierreCaja> CierresCaja { get; set; }
    public DbSet<MovimientoInventario> MovimientosInventario { get; set; }
    public DbSet<Proveedor> Proveedores { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de Mesa
        modelBuilder.Entity<Mesa>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Numero).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Capacidad).HasDefaultValue(4);
            entity.Property(e => e.Estado).IsRequired().HasMaxLength(50).HasDefaultValue("Libre");
            entity.HasIndex(e => e.Numero).IsUnique();
            
            entity.HasOne(e => e.Ubicacion)
                .WithMany(u => u.Mesas)
                .HasForeignKey(e => e.UbicacionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuración de CategoriaProducto
        modelBuilder.Entity<CategoriaProducto>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.Property(e => e.ColorHex).HasMaxLength(20);
            entity.Property(e => e.IconoNombre).HasMaxLength(100);
            entity.Property(e => e.Orden).HasDefaultValue(0);
            entity.Property(e => e.RequiereCocina).HasDefaultValue(true);
            entity.HasIndex(e => e.Nombre).IsUnique();
        });

        // Configuración de Cliente
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Codigo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Telefono).HasMaxLength(20);
            entity.Property(e => e.Cedula).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.TotalFacturas).HasDefaultValue(0);
            entity.HasIndex(e => e.Codigo).IsUnique();
            
            // Relación opcional con Servicio (último servicio usado)
            entity.HasOne(e => e.Servicio)
                .WithMany()
                .HasForeignKey(e => e.ServicioId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuración de Servicio (Producto)
        modelBuilder.Entity<Servicio>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Codigo).HasMaxLength(50);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.Property(e => e.Precio).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PrecioCompra).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            entity.Property(e => e.Categoria).IsRequired().HasMaxLength(50).HasDefaultValue("Bebidas");
            entity.Property(e => e.Stock).HasDefaultValue(0);
            entity.Property(e => e.StockMinimo).HasDefaultValue(0);
            entity.Property(e => e.ControlarStock).HasDefaultValue(false);
            entity.Property(e => e.EsPreparado).HasDefaultValue(true);
            entity.Property(e => e.ImagenUrl).HasMaxLength(500);
            entity.Property(e => e.Destacado).HasDefaultValue(false);
            entity.HasIndex(e => e.Codigo).IsUnique().HasFilter("\"Codigo\" IS NOT NULL AND \"Codigo\" != ''");
            
            entity.HasOne(e => e.CategoriaProducto)
                .WithMany(c => c.Productos)
                .HasForeignKey(e => e.CategoriaProductoId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuración de Factura (Orden)
        modelBuilder.Entity<Factura>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Numero).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Monto).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Estado).IsRequired().HasMaxLength(50).HasDefaultValue("Pendiente");
            entity.Property(e => e.EstadoCocina).IsRequired().HasMaxLength(50).HasDefaultValue("Pendiente");
            entity.Property(e => e.Categoria).IsRequired().HasMaxLength(50).HasDefaultValue("General");
            entity.Property(e => e.OrigenPedido).IsRequired().HasMaxLength(20).HasDefaultValue("Salon");
            entity.Property(e => e.DeliveryClienteNombre).HasMaxLength(200);
            entity.Property(e => e.DeliveryClienteTelefono).HasMaxLength(40);
            entity.Property(e => e.DeliveryClienteDireccion).HasMaxLength(500);
            entity.Property(e => e.ArchivoPDF).HasMaxLength(500);
            entity.Property(e => e.Observaciones).HasMaxLength(1000);
            entity.Property(e => e.TiempoPreparacion).HasDefaultValue(0);
            
            entity.HasOne(e => e.Mesa)
                .WithMany(m => m.Ordenes)
                .HasForeignKey(e => e.MesaId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Facturas)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Mesero)
                .WithMany()
                .HasForeignKey(e => e.MeseroId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Servicio)
                .WithMany(s => s.Facturas)
                .HasForeignKey(e => e.ServicioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuración de FacturaServicio (OrdenProducto)
        modelBuilder.Entity<FacturaServicio>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Cantidad).IsRequired().HasDefaultValue(1);
            entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Monto).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Notas).HasMaxLength(500);
            entity.Property(e => e.Estado).IsRequired().HasMaxLength(50).HasDefaultValue("Pendiente");
            
            entity.HasOne(e => e.Factura)
                .WithMany(f => f.FacturaServicios)
                .HasForeignKey(e => e.FacturaId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Servicio)
                .WithMany()
                .HasForeignKey(e => e.ServicioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductoOpcionGrupo>(entity =>
        {
            entity.ToTable("ProductoOpcionGrupos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(120);
            entity.Property(e => e.MinSeleccion).HasDefaultValue(0);
            entity.Property(e => e.MaxSeleccion).HasDefaultValue(1);
            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.HasOne(e => e.Servicio)
                .WithMany(s => s.OpcionGrupos)
                .HasForeignKey(e => e.ServicioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductoOpcionItem>(entity =>
        {
            entity.ToTable("ProductoOpcionItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(120);
            entity.Property(e => e.PrecioAdicional).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.HasOne(e => e.Grupo)
                .WithMany(g => g.Opciones)
                .HasForeignKey(e => e.GrupoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FacturaServicioOpcionSeleccion>(entity =>
        {
            entity.ToTable("OrdenLineaOpciones");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NombreGrupo).IsRequired().HasMaxLength(120);
            entity.Property(e => e.NombreOpcion).IsRequired().HasMaxLength(120);
            entity.Property(e => e.PrecioAdicional).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            entity.HasOne(e => e.FacturaServicio)
                .WithMany(fs => fs.OpcionesSeleccionadas)
                .HasForeignKey(e => e.FacturaServicioId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.ProductoOpcionItemId);
        });

        // Configuración de Pago
        modelBuilder.Entity<Pago>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Monto).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Moneda).IsRequired().HasMaxLength(10);
            entity.Property(e => e.TipoPago).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Banco).HasMaxLength(100);
            entity.Property(e => e.TipoCuenta).HasMaxLength(100);
            entity.Property(e => e.MontoRecibido).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Vuelto).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TipoCambio).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Observaciones).HasMaxLength(500);
            entity.Property(e => e.DescuentoMonto).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            entity.Property(e => e.DescuentoMotivo).HasMaxLength(500);
            
            // Campos para pago físico con múltiples monedas
            entity.Property(e => e.MontoCordobasFisico).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoDolaresFisico).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoRecibidoFisico).HasColumnType("decimal(18,2)");
            entity.Property(e => e.VueltoFisico).HasColumnType("decimal(18,2)");
            
            // Campos para pago electrónico con múltiples monedas
            entity.Property(e => e.MontoCordobasElectronico).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoDolaresElectronico).HasColumnType("decimal(18,2)");
            
            // Relación con Factura (opcional, para compatibilidad con pagos de una sola factura)
            entity.HasOne(e => e.Factura)
                .WithMany(f => f.Pagos)
                .HasForeignKey(e => e.FacturaId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false); // Permitir null para pagos con múltiples facturas
            
            // Relación con PagoFactura (para pagos con múltiples facturas)
            entity.HasMany(e => e.PagoFacturas)
                .WithOne(pf => pf.Pago)
                .HasForeignKey(pf => pf.PagoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de PagoFactura
        modelBuilder.Entity<PagoFactura>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MontoAplicado).HasColumnType("decimal(18,2)");
            
            entity.HasOne(e => e.Pago)
                .WithMany(p => p.PagoFacturas)
                .HasForeignKey(e => e.PagoId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Factura)
                .WithMany()
                .HasForeignKey(e => e.FacturaId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Índice compuesto para evitar duplicados
            entity.HasIndex(e => new { e.PagoId, e.FacturaId }).IsUnique();
        });

        // Configuración de Usuario
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NombreUsuario).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Contrasena).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Rol).IsRequired().HasMaxLength(50);
            entity.Property(e => e.NombreCompleto).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.NombreUsuario).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(200);
            entity.Property(e => e.JwtId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.ReemplazadoPorTokenHash).HasMaxLength(200);
            entity.Property(e => e.MotivoRevocacion).HasMaxLength(500);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => new { e.UsuarioId, e.ExpiraEnUtc });

            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de ClienteServicio (relación muchos-a-muchos)
        modelBuilder.Entity<ClienteServicio>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Cantidad).IsRequired().HasDefaultValue(1);
            
            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.ClienteServicios)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Servicio)
                .WithMany(s => s.ClienteServicios)
                .HasForeignKey(e => e.ServicioId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Índice compuesto para mejorar búsquedas
            entity.HasIndex(e => new { e.ClienteId, e.ServicioId });
        });

        // Configuración de PlantillaMensajeWhatsApp
        modelBuilder.Entity<PlantillaMensajeWhatsApp>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Mensaje).IsRequired().HasColumnType("text");
            entity.Property(e => e.Activa).HasDefaultValue(true);
            entity.Property(e => e.EsDefault).HasDefaultValue(false);
        });

        // Configuración de Configuracion
        modelBuilder.Entity<Configuracion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Clave).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Valor).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.Property(e => e.UsuarioActualizacion).HasMaxLength(200);
            entity.HasIndex(e => e.Clave).IsUnique(); // Clave única para evitar duplicados
        });

        // Configuración de Ubicacion
        modelBuilder.Entity<Ubicacion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.HasIndex(e => e.Nombre).IsUnique();
        });

        // Configuración de CierreCaja
        modelBuilder.Entity<CierreCaja>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MontoInicial).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalEfectivo).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalTarjeta).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalTransferencia).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalCordobas).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalDolares).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalGeneral).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoEsperado).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoReal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Diferencia).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Observaciones).HasMaxLength(1000);
            entity.Property(e => e.Estado).IsRequired().HasMaxLength(50).HasDefaultValue("Cerrado");
            
            // Índice único por fecha para evitar cierres duplicados
            entity.HasIndex(e => e.FechaCierre).IsUnique();
            
            // Relación con Usuario
            entity.HasOne(e => e.Usuario)
                .WithMany()
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuración de Proveedor
        modelBuilder.Entity<Proveedor>(entity =>
        {
            entity.ToTable("Proveedores");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Telefono).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.Direccion).HasMaxLength(500);
            entity.Property(e => e.Contacto).HasMaxLength(200);
            entity.Property(e => e.Observaciones).HasMaxLength(1000);
            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.Nombre).IsUnique();
        });

        // Configuración de MovimientoInventario
        modelBuilder.Entity<MovimientoInventario>(entity =>
        {
            entity.ToTable("MovimientosInventario");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Tipo).IsRequired().HasMaxLength(50); // Entrada, Salida
            entity.Property(e => e.Subtipo).IsRequired().HasMaxLength(50); // Compra, Venta, Daño, Ajuste, etc.
            entity.Property(e => e.Cantidad).IsRequired();
            entity.Property(e => e.CostoUnitario).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CostoTotal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Fecha).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.NumeroFactura).HasMaxLength(100);
            entity.Property(e => e.Observaciones).HasMaxLength(1000);
            entity.Property(e => e.StockAnterior).IsRequired();
            entity.Property(e => e.StockNuevo).IsRequired();
            
            // Relación con Producto (Servicio)
            entity.HasOne(e => e.Producto)
                .WithMany()
                .HasForeignKey(e => e.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Relación con Usuario
            entity.HasOne(e => e.Usuario)
                .WithMany()
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Relación con Proveedor (opcional)
            entity.HasOne(e => e.Proveedor)
                .WithMany(p => p.MovimientosInventario)
                .HasForeignKey(e => e.ProveedorId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Relación con Factura (opcional, para ventas)
            entity.HasOne(e => e.Factura)
                .WithMany()
                .HasForeignKey(e => e.FacturaId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Índices para mejorar búsquedas
            entity.HasIndex(e => e.ProductoId);
            entity.HasIndex(e => e.Fecha);
            entity.HasIndex(e => e.Tipo);
            entity.HasIndex(e => new { e.ProductoId, e.Fecha });
        });
    }
}

