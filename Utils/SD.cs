namespace BarRestPOS.Utils;

public static class SD
{
    // ========== ROLES DE USUARIO ==========
    public const string RolAdministrador = "Administrador";
    public const string RolMesero = "Mesero";
    public const string RolCocinero = "Cocinero";
    public const string RolCajero = "Cajero";
    public const string RolBartender = "Bartender";
    
    // Roles antiguos (mantener por compatibilidad)
    public const string RolNormal = "Normal";
    public const string RolCaja = "Caja";

    // ========== ESTADOS DE ORDEN ==========
    public const string EstadoOrdenPendiente = "Pendiente";
    public const string EstadoOrdenEnCocina = "En Cocina";
    public const string EstadoOrdenListo = "Listo";
    public const string EstadoOrdenServido = "Servido";
    public const string EstadoOrdenPagado = "Pagado";
    public const string EstadoOrdenCancelado = "Cancelado";

    /// <summary>Pedido creado (delivery u operación) sin cobrar aún.</summary>
    public const string EstadoOrdenGuardado = "Guardado";

    /// <summary>Origen del pedido (Factura.OrigenPedido).</summary>
    public const string OrigenPedidoSalon = "Salon";
    public const string OrigenPedidoDelivery = "Delivery";
    /// <summary>Para llevar (sin mesa o mesa virtual según operación).</summary>
    public const string OrigenPedidoLlevar = "Llevar";

    /// <summary>Clave en Configuraciones para el PIN de cancelación de pedidos.</summary>
    public const string ConfigClavePinCancelacionPedidos = "PinCancelacionPedidos";
    
    // Estados de Cocina (KDS - Kitchen Display System)
    public const string EstadoCocinaPendiente = "Pendiente";
    public const string EstadoCocinaEnPreparacion = "En Preparación";
    public const string EstadoCocinaListo = "Listo";
    public const string EstadoCocinaEntregado = "Entregado";
    
    // Estados antiguos (mantener por compatibilidad)
    public const string EstadoFacturaPendiente = "Pendiente";
    public const string EstadoFacturaPagada = "Pagada";
    public const string EstadoFacturaCancelada = "Cancelada";

    // ========== ESTADOS DE MESA ==========
    public const string EstadoMesaLibre = "Libre";
    public const string EstadoMesaOcupada = "Ocupada";
    public const string EstadoMesaReservada = "Reservada";

    // Tipos de Pago
    public const string TipoPagoFisico = "Fisico";
    public const string TipoPagoElectronico = "Electronico";
    public const string TipoPagoMixto = "Mixto";

    // Monedas
    public const string MonedaCordoba = "C$";
    public const string MonedaDolar = "$";
    public const string MonedaAmbos = "Ambos";

    // Tipo de Cambio
    public const decimal TipoCambioDolar = 36.80m; // C$36.80 = $1

    // Bancos
    public const string BancoBanpro = "Banpro";
    public const string BancoLafise = "Lafise";
    public const string BancoBAC = "BAC";
    public const string BancoFicohsa = "Ficohsa";
    public const string BancoBDF = "BDF";

    // Tipos de Cuenta
    public const string TipoCuentaDolar = "Cuenta $";
    public const string TipoCuentaCordoba = "Cuenta C$";
    public const string TipoCuentaBilletera = "Billetera movil";

    // ========== CATEGORÍAS DE PRODUCTO ==========
    public const string CategoriaBebidas = "Bebidas";
    public const string CategoriaComidas = "Comidas";
    public const string CategoriaLicores = "Licores";
    public const string CategoriaPostres = "Postres";
    public const string CategoriaPromos = "Promos";
    public const string CategoriaEntradas = "Entradas";
    public const string CategoriaCocteles = "Cócteles";
    public const string CategoriaCervezas = "Cervezas";
    
    // Categorías antiguas (mantener por compatibilidad)
    public const string CategoriaInternet = "Internet";
    public const string CategoriaStreaming = "Streaming";
    
    // ========== ÁREAS/UBICACIONES DEL RESTAURANTE ==========
    public const string UbicacionSalon = "Salón";
    public const string UbicacionTerraza = "Terraza";
    public const string UbicacionVIP = "VIP";
    public const string UbicacionBar = "Bar";
    public const string UbicacionCocina = "Cocina";

    // Estados de Equipo (Inventario)
    public const string EstadoEquipoDisponible = "Disponible";
    public const string EstadoEquipoEnUso = "En uso";
    public const string EstadoEquipoDanado = "Dañado";
    public const string EstadoEquipoEnReparacion = "En reparación";
    public const string EstadoEquipoRetirado = "Retirado";

    // Tipos de Movimiento de Inventario
    public const string TipoMovimientoEntrada = "Entrada";
    public const string TipoMovimientoSalida = "Salida";

    // Subtipos de Movimiento de Inventario
    public const string SubtipoMovimientoCompra = "Compra";
    public const string SubtipoMovimientoVenta = "Venta";
    public const string SubtipoMovimientoAsignacion = "Asignación";
    public const string SubtipoMovimientoDevolucion = "Devolución";
    public const string SubtipoMovimientoAjuste = "Ajuste";
    public const string SubtipoMovimientoDano = "Daño";
    public const string SubtipoMovimientoTransferencia = "Transferencia";

    // Estados de Asignación de Equipo
    public const string EstadoAsignacionActiva = "Activa";
    public const string EstadoAsignacionDevuelta = "Devuelta";
    public const string EstadoAsignacionPerdida = "Perdida";

    // Tipos de Mantenimiento
    public const string TipoMantenimientoPreventivo = "Preventivo";
    public const string TipoMantenimientoCorrectivo = "Correctivo";

    // Estados de Mantenimiento
    public const string EstadoMantenimientoProgramado = "Programado";
    public const string EstadoMantenimientoEnProceso = "En proceso";
    public const string EstadoMantenimientoCompletado = "Completado";
    public const string EstadoMantenimientoCancelado = "Cancelado";

    // Tipos de Ubicación (Inventario)
    public const string TipoUbicacionAlmacen = "Almacen";
    public const string TipoUbicacionCampo = "Campo";
    public const string TipoUbicacionReparacion = "Reparacion";
    
    // ========== TIPOS DE CLIENTE ==========
    public const string TipoClienteGeneral = "General";
    public const string TipoClienteFrecuente = "Frecuente";
    public const string TipoClienteVIP = "VIP";
    
    // ========== MÉTODOS DE PAGO RÁPIDO ==========
    public static class MetodosPagoRapido
    {
        public const string Efectivo = "Efectivo";
        public const string Tarjeta = "Tarjeta";
        public const string Transferencia = "Transferencia";
        public const string Sinpe = "SINPE Móvil";
        public const string Mixto = "Mixto";
    }

    // Servicios Principales (mantener por compatibilidad)
    public static class ServiciosPrincipales
    {
        public const string Servicio1 = "Servicio 1";
        public const string Servicio2 = "Servicio 2";
        public const string Servicio3 = "Servicio 3";
        public const string ServicioEspecial = "Especial";

        public const decimal PrecioServicio1 = 920m;
        public const decimal PrecioServicio2 = 1104m;
        public const decimal PrecioServicio3 = 1288m;
        public const decimal PrecioServicioEspecial = 1000m;
    }
    
    // ========== PRODUCTOS DESTACADOS ==========
    public static class ProductosDestacados
    {
        public const string CervezaNacional = "Cerveza Nacional";
        public const string CervezaImportada = "Cerveza Importada";
        public const string Gaseosa = "Gaseosa";
        public const string Agua = "Agua";
        public const string PlatoDelDia = "Plato del Día";
        
        public const decimal PrecioCervezaNacional = 50m; // C$50
        public const decimal PrecioCervezaImportada = 80m; // C$80
        public const decimal PrecioGaseosa = 30m; // C$30
        public const decimal PrecioAgua = 20m; // C$20
        public const decimal PrecioPlatoDelDia = 200m; // C$200
    }

    // Usuarios estáticos (temporal, hasta conectar BD)
    public static class UsuariosEstaticos
    {
        public static List<Models.Entities.Usuario> ObtenerUsuarios()
        {
            return new List<Models.Entities.Usuario>
            {
                new Models.Entities.Usuario
                {
                    Id = 1,
                    NombreUsuario = "admin",
                    Contrasena = "admin", // En producción debe estar hasheada
                    Rol = RolAdministrador,
                    NombreCompleto = "Administrador del Sistema",
                    Activo = true
                },
                new Models.Entities.Usuario
                {
                    Id = 2,
                    NombreUsuario = "usuario",
                    Contrasena = "usuario",
                    Rol = RolNormal,
                    NombreCompleto = "Usuario Normal",
                    Activo = true
                }
            };
        }
    }
}

