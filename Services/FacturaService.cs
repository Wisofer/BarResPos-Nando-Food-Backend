using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class FacturaService : IFacturaService
{
    private readonly ApplicationDbContext _context;
    private readonly IClienteService _clienteService;
    private readonly IServicioService _servicioService;

    public FacturaService(ApplicationDbContext context, IClienteService clienteService, IServicioService servicioService)
    {
        _context = context;
        _clienteService = clienteService;
        _servicioService = servicioService;
    }

    public List<Factura> ObtenerTodas()
    {
        return _context.Facturas
            .Include(f => f.Cliente)
            .Include(f => f.Servicio)
            .OrderByDescending(f => f.FechaCreacion)
            .ToList();
    }

    public Factura? ObtenerPorId(int id)
    {
        var factura = _context.Facturas
            .Include(f => f.Cliente)
                .ThenInclude(c => c!.ClienteServicios)
            .Include(f => f.Servicio)
            .Include(f => f.FacturaServicios)
                .ThenInclude(fs => fs.Servicio)
            .FirstOrDefault(f => f.Id == id);
        
        if (factura != null)
        {
            // Cargar pagos relacionados: directos (FacturaId) e indirectos (a través de PagoFacturas)
            var pagosDirectos = _context.Pagos
                .Where(p => p.FacturaId == factura.Id)
                .ToList();
            
            var pagosIndirectos = _context.PagoFacturas
                .Where(pf => pf.FacturaId == factura.Id)
                .Include(pf => pf.Pago)
                .Select(pf => pf.Pago)
                .ToList();
            
            // Combinar ambos tipos de pagos y eliminar duplicados
            var todosLosPagos = pagosDirectos
                .Union(pagosIndirectos)
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .OrderBy(p => p.FechaPago)
                .ToList();
            
            // Asignar los pagos a la factura
            factura.Pagos = todosLosPagos;
        }
        
        return factura;
    }

    public List<Factura> ObtenerPorCliente(int clienteId)
    {
        return _context.Facturas
            .Include(f => f.Cliente)
            .Include(f => f.Servicio)
            .Where(f => f.ClienteId == clienteId)
            .OrderByDescending(f => f.FechaCreacion)
            .ToList();
    }

    public List<Factura> ObtenerPorMes(DateTime mes)
    {
        return _context.Facturas
            .Include(f => f.Cliente)
            .Include(f => f.Servicio)
            .Where(f => f.MesFacturacion.Year == mes.Year && f.MesFacturacion.Month == mes.Month)
            .OrderByDescending(f => f.FechaCreacion)
            .ToList();
    }

    public List<Factura> ObtenerPendientes()
    {
        return _context.Facturas
            .Include(f => f.Cliente)
            .Include(f => f.Servicio)
            .Where(f => f.Estado == SD.EstadoFacturaPendiente)
            .OrderByDescending(f => f.FechaCreacion)
            .ToList();
    }

    public string GenerarNumeroFactura(Cliente? cliente, DateTime mes, string? categoria = null)
    {
        var mesStr = mes.ToString("MM");
        var añoStr = mes.ToString("yyyy");
        
        // Si no hay cliente (restaurante), usar formato ORD-XXXXX
        if (cliente == null)
        {
            // Buscar el número más alto de orden existente
            var ultimaOrden = _context.Facturas
                .Where(f => f.Numero.StartsWith("ORD-"))
                .OrderByDescending(f => f.Id)
                .FirstOrDefault();
            
            // Si no hay órdenes previas, empezar desde 1
            // Si hay, usar el ID más alto + 1
            var numeroOrden = ultimaOrden != null ? ultimaOrden.Id + 1 : 1;
            return $"ORD-{numeroOrden:D5}";
        }
        
        var nombreSinEspacios = cliente.Nombre.Replace(" ", "");
        var longitudNombre = Math.Min(10, nombreSinEspacios.Length);
        var nombreCliente = longitudNombre > 0 ? nombreSinEspacios.Substring(0, longitudNombre) : "Cliente";
        var sufijo = categoria == SD.CategoriaStreaming ? "-STR" : "";
        
        // Obtener todas las facturas
        var todasLasFacturas = _context.Facturas
            .Select(f => f.Numero)
            .ToList();
        
        int numero = 1;
        
        if (categoria == SD.CategoriaStreaming)
        {
            // STREAMING: Numeración propia separada (busca solo facturas con -STR)
            int numeroMaximoSTR = 0;
            foreach (var numeroFactura in todasLasFacturas)
            {
                if (!string.IsNullOrEmpty(numeroFactura) && numeroFactura.EndsWith("-STR"))
                {
                    // Extraer el número del formato "XXXX-Nombre-MMYYYY-STR"
                    var partes = numeroFactura.Split('-');
                    if (partes.Length > 0 && int.TryParse(partes[0], out var num))
                    {
                        if (num > numeroMaximoSTR)
                        {
                            numeroMaximoSTR = num;
                        }
                    }
                }
            }
            // Streaming: continuar desde el más alto encontrado (o empezar desde 1)
            numero = numeroMaximoSTR + 1;
        }
        else
        {
            // INTERNET: Numeración secuencial global desde 179 (solo facturas sin -STR)
            int numeroMaximoInternet = 0;
            foreach (var numeroFactura in todasLasFacturas)
            {
                if (!string.IsNullOrEmpty(numeroFactura) && !numeroFactura.EndsWith("-STR"))
                {
                    // Extraer el número del formato "XXXX-Nombre-MMYYYY"
                    var partes = numeroFactura.Split('-');
                    if (partes.Length > 0 && int.TryParse(partes[0], out var num))
                    {
                        if (num > numeroMaximoInternet)
                        {
                            numeroMaximoInternet = num;
                        }
                    }
                }
            }
            // Internet: Si el número máximo es >= 179, continuar la secuencia, sino empezar desde 179
            numero = numeroMaximoInternet >= 179 ? numeroMaximoInternet + 1 : 179;
        }
        
        return $"{numero:D4}-{nombreCliente}-{mesStr}{añoStr}{sufijo}";
    }

    /// <summary>
    /// Calcula el monto de la factura aplicando lógica proporcional para clientes nuevos.
    /// El proporcional se aplica SOLO si:
    /// 1. Es la primera factura del cliente (usuario nuevo)
    /// 2. El cliente se creó en el mes de facturación
    /// 3. El cliente se creó después del día 5
    /// El ciclo de facturación es del día 5 al día 5 (30 días).
    /// </summary>
    private decimal CalcularMontoProporcional(Cliente cliente, Servicio servicio, DateTime mesFacturacion)
    {
        // Verificar si es la primera factura del cliente
        var esPrimeraFactura = !_context.Facturas.Any(f => f.ClienteId == cliente.Id);

        // REGLA: Si NO es la primera factura (usuario viejo), SIEMPRE paga mes completo
        if (!esPrimeraFactura)
        {
            return servicio.Precio;
        }

        // Si es la primera factura (usuario nuevo), usar la misma lógica unificada
        return CalcularMontoProporcionalConFechaInicio(cliente, servicio, mesFacturacion, cliente.FechaCreacion);
    }

    public Factura Crear(Factura factura)
    {
        // VALIDACIÓN CRÍTICA: Si es una orden de restaurante (Categoria = "General" o tiene MesaId),
        // verificar que la caja esté abierta
        if ((factura.Categoria == "General" || factura.MesaId.HasValue) && factura.ClienteId == null)
        {
            var hoy = DateTime.Today;
            var cierreHoy = _context.CierresCaja
                .FirstOrDefault(c => c.FechaCierre.Date == hoy && c.Estado == "Abierto");
            
            if (cierreHoy == null)
            {
                throw new Exception("La caja está cerrada. Debe abrir la caja antes de crear órdenes.");
            }
        }

        // Validar cliente si se proporciona (en restaurante el cliente puede ser opcional/general)
        Cliente? cliente = null;
        if (factura.ClienteId.HasValue)
        {
            cliente = _clienteService.ObtenerPorId(factura.ClienteId.Value);
            if (cliente == null)
                throw new Exception("Cliente no encontrado");
        }

        var servicio = _servicioService.ObtenerPorId(factura.ServicioId);
        if (servicio == null)
            throw new Exception("Producto no encontrado");

        // Generar número de orden (factura) solo si no está establecido
        if (string.IsNullOrEmpty(factura.Numero))
        {
            factura.Numero = GenerarNumeroFactura(cliente, factura.MesFacturacion, servicio.Categoria);
        }
        
        // Calcular monto: Streaming sin proporcional, Internet con proporcional
        // Si el monto ya está establecido (restaurante con múltiples productos), no sobrescribirlo
        if (factura.Monto > 0 && cliente == null)
        {
            // Restaurante: el monto ya fue calculado en el controlador, no sobrescribir
        }
        else if (servicio.Categoria == SD.CategoriaStreaming && cliente != null)
        {
            // Streaming: siempre precio completo (precio * cantidad)
            var clienteServicio = _context.ClienteServicios
                .FirstOrDefault(cs => cs.ClienteId == cliente.Id && cs.ServicioId == servicio.Id && cs.Activo);
            var cantidad = clienteServicio?.Cantidad ?? 1;
            factura.Monto = servicio.Precio * cantidad;
        }
        else if (cliente != null)
        {
            // Internet: aplicar proporcional
            factura.Monto = CalcularMontoProporcional(cliente, servicio, factura.MesFacturacion);
        }
        else
        {
            // Restaurante: precio simple del producto (solo si el monto no fue establecido)
            if (factura.Monto == 0)
            {
                factura.Monto = servicio.Precio;
            }
        }
        
        factura.Estado = SD.EstadoOrdenPendiente;
        factura.FechaCreacion = DateTime.Now;

        _context.Facturas.Add(factura);
        
        // Actualizar el último servicio usado del cliente (solo si tiene cliente)
        if (cliente != null)
        {
            cliente.ServicioId = factura.ServicioId;
        }
        
        // NO incrementar TotalFacturas aquí - solo se incrementa cuando la factura se marca como Pagada
        
        _context.SaveChanges();
        return factura;
    }

    public List<Factura> CrearFacturasAgrupadasPorCategoria(int clienteId, List<int> servicioIds, DateTime mesFacturacion)
    {
        var cliente = _clienteService.ObtenerPorId(clienteId);
        if (cliente == null)
            throw new Exception("Cliente no encontrado");

        var facturasCreadas = new List<Factura>();

        // Obtener servicios y agrupar por categoría
        // IMPORTANTE: Filtrar servicios válidos y evitar duplicados
        var serviciosConInfo = servicioIds
            .Select(id => new
            {
                ServicioId = id,
                Servicio = _servicioService.ObtenerPorId(id),
                ClienteServicio = _context.ClienteServicios
                    .FirstOrDefault(cs => cs.ClienteId == clienteId && cs.ServicioId == id && cs.Activo)
            })
            .Where(x => x.Servicio != null && 
                       x.Servicio.Activo && 
                       !string.IsNullOrWhiteSpace(x.Servicio.Categoria))
            .GroupBy(x => x.ServicioId) // Agrupar por ServicioId para evitar duplicados
            .Select(g => g.First()) // Tomar el primero si hay duplicados
            .GroupBy(x => x.Servicio!.Categoria) // Luego agrupar por categoría
            .ToList();

        foreach (var grupoCategoria in serviciosConInfo)
        {
            var categoria = grupoCategoria.Key;
            var serviciosDelGrupo = grupoCategoria.ToList();

            // Validar que la categoría sea válida
            if (categoria != SD.CategoriaInternet && categoria != SD.CategoriaStreaming)
            {
                continue; // Saltar categorías inválidas
            }

            // Verificar si ya existe factura para esta categoría en este mes
            var existeFactura = _context.Facturas.Any(f =>
                f.ClienteId == clienteId &&
                f.Categoria == categoria &&
                f.MesFacturacion.Year == mesFacturacion.Year &&
                f.MesFacturacion.Month == mesFacturacion.Month);

            if (existeFactura)
            {
                continue; // Ya existe factura para esta categoría
            }

            // Calcular montos y crear FacturaServicios
            decimal montoTotal = 0;
            var primerServicio = serviciosDelGrupo.First().Servicio!;
            var facturaServicios = new List<FacturaServicio>();

            foreach (var item in serviciosDelGrupo)
            {
                var servicio = item.Servicio!;
                var clienteServicio = item.ClienteServicio;
                int cantidad = 1;
                
                // Para Streaming, obtener la cantidad de suscripciones
                if (servicio.Categoria == SD.CategoriaStreaming && clienteServicio != null)
                {
                    cantidad = clienteServicio.Cantidad;
                }
                
                // Calcular monto del servicio
                var montoServicio = CalcularMontoServicio(servicio, cliente, mesFacturacion, cantidad);
                
                montoTotal += montoServicio;

                // Crear FacturaServicio - guardar el monto total y la cantidad
                facturaServicios.Add(new FacturaServicio
                {
                    ServicioId = servicio.Id,
                    Cantidad = cantidad,
                    Monto = montoServicio
                });
            }

            // Crear la factura consolidada
            var factura = new Factura
            {
                ClienteId = clienteId,
                ServicioId = primerServicio.Id, // Servicio principal para compatibilidad
                Categoria = categoria,
                MesFacturacion = mesFacturacion,
                Numero = GenerarNumeroFactura(cliente, mesFacturacion, categoria),
                Monto = montoTotal,
                Estado = SD.EstadoFacturaPendiente,
                FechaCreacion = DateTime.Now,
                FacturaServicios = facturaServicios
            };

            _context.Facturas.Add(factura);
            facturasCreadas.Add(factura);
        }

        _context.SaveChanges();
        return facturasCreadas;
    }

    public Factura Actualizar(Factura factura)
    {
        var existente = ObtenerPorId(factura.Id);
        if (existente == null)
            throw new Exception("Factura no encontrada");

        var estadoAnterior = existente.Estado;
        var estadoNuevo = factura.Estado;

        // Solo actualizar campos permitidos
        existente.Estado = factura.Estado;
        if (!string.IsNullOrWhiteSpace(factura.ArchivoPDF))
        {
            existente.ArchivoPDF = factura.ArchivoPDF;
        }

        // Incrementar TotalFacturas solo cuando la factura cambia a estado "Pagada"
        if (estadoAnterior != SD.EstadoFacturaPagada && estadoNuevo == SD.EstadoFacturaPagada && existente.ClienteId.HasValue)
        {
            var cliente = _clienteService.ObtenerPorId(existente.ClienteId.Value);
            if (cliente != null)
            {
                cliente.TotalFacturas++;
            }
        }
        // Decrementar si cambia de Pagada a otro estado (aunque esto no debería pasar normalmente)
        else if (estadoAnterior == SD.EstadoFacturaPagada && estadoNuevo != SD.EstadoFacturaPagada && existente.ClienteId.HasValue)
        {
            var cliente = _clienteService.ObtenerPorId(existente.ClienteId.Value);
            if (cliente != null && cliente.TotalFacturas > 0)
            {
                cliente.TotalFacturas--;
            }
        }

        _context.SaveChanges();
        return existente;
    }

    public bool Eliminar(int id)
    {
        var factura = ObtenerPorId(id);
        if (factura == null)
            return false;

        // Verificar si tiene pagos (directos o a través de PagoFactura)
        var tienePagos = _context.Pagos.Any(p => p.FacturaId == id) ||
                        _context.PagoFacturas.Any(pf => pf.FacturaId == id);
        if (tienePagos)
            return false; // No se puede eliminar si tiene pagos

        // Decrementar TotalFacturas solo si la factura estaba pagada y tiene cliente
        if (factura.Estado == SD.EstadoFacturaPagada && factura.ClienteId.HasValue)
        {
            var cliente = _clienteService.ObtenerPorId(factura.ClienteId.Value);
            if (cliente != null && cliente.TotalFacturas > 0)
            {
                cliente.TotalFacturas--;
            }
        }

        _context.Facturas.Remove(factura);
        _context.SaveChanges();
        return true;
    }

    public (int eliminadas, int conPagos, int noEncontradas) EliminarMultiples(List<int> ids)
    {
        if (ids == null || !ids.Any())
            return (0, 0, 0);

        int eliminadas = 0;
        int conPagos = 0;
        int noEncontradas = 0;

        foreach (var id in ids)
        {
            var factura = ObtenerPorId(id);
            if (factura == null)
            {
                noEncontradas++;
                continue;
            }

            // Verificar si tiene pagos (directos o a través de PagoFactura)
            var tienePagos = _context.Pagos.Any(p => p.FacturaId == id) ||
                            _context.PagoFacturas.Any(pf => pf.FacturaId == id);
            if (tienePagos)
            {
                conPagos++;
                continue;
            }

            // Decrementar TotalFacturas solo si la factura estaba pagada y tiene cliente
            if (factura.Estado == SD.EstadoFacturaPagada && factura.ClienteId.HasValue)
            {
                var cliente = _clienteService.ObtenerPorId(factura.ClienteId.Value);
                if (cliente != null && cliente.TotalFacturas > 0)
                {
                    cliente.TotalFacturas--;
                }
            }

            _context.Facturas.Remove(factura);
            eliminadas++;
        }

        if (eliminadas > 0)
        {
            _context.SaveChanges();
        }

        return (eliminadas, conPagos, noEncontradas);
    }

    public void GenerarFacturasAutomaticas()
    {
        var clientes = _clienteService.ObtenerTodos().Where(c => c.Activo).ToList();
        // CORRECCIÓN: Facturar el mes anterior (primero se consume, luego se paga)
        // Si estamos en diciembre, facturamos noviembre
        var mesFacturacion = DateTime.Now.AddMonths(-1);
        mesFacturacion = new DateTime(mesFacturacion.Year, mesFacturacion.Month, 1);

        if (!clientes.Any())
        {
            return; // No hay clientes activos
        }

        var facturasACrear = new List<Factura>();

        foreach (var cliente in clientes)
        {
            // Obtener todos los servicios activos del cliente
            var serviciosActivos = _clienteService.ObtenerServiciosActivos(cliente.Id);

            if (!serviciosActivos.Any())
            {
                continue; // Saltar clientes sin servicios activos
            }

            // Filtrar servicios válidos (con servicio activo y categoría válida)
            var serviciosValidos = serviciosActivos
                .Where(cs => cs.Servicio != null && 
                            cs.Servicio.Activo && 
                            !string.IsNullOrWhiteSpace(cs.Servicio.Categoria))
                .ToList();

            if (!serviciosValidos.Any())
            {
                continue; // Saltar si no hay servicios válidos
            }

            // IMPORTANTE: Agrupar por ServicioId primero para evitar duplicados
            // Si hay múltiples ClienteServicio con el mismo ServicioId, solo tomar uno
            var serviciosUnicos = serviciosValidos
                .GroupBy(cs => cs.ServicioId)
                .Select(g => g.OrderBy(cs => cs.FechaInicio).First()) // Tomar el más antiguo si hay duplicados
                .ToList();

            // Agrupar servicios únicos por categoría
            var serviciosPorCategoria = serviciosUnicos
                .GroupBy(cs => cs.Servicio!.Categoria)
                .ToList();

            foreach (var grupoCategoria in serviciosPorCategoria)
            {
                var categoria = grupoCategoria.Key;
                var serviciosDelGrupo = grupoCategoria.ToList();

                // Validar que la categoría sea válida
                if (categoria != SD.CategoriaInternet && categoria != SD.CategoriaStreaming)
                {
                    continue; // Saltar categorías inválidas
                }

                // Verificar si ya existe factura para este cliente y categoría en este mes
                var existeFactura = _context.Facturas.Any(f =>
                    f.ClienteId == cliente.Id &&
                    f.Categoria == categoria &&
                    f.MesFacturacion.Year == mesFacturacion.Year &&
                    f.MesFacturacion.Month == mesFacturacion.Month);

                if (existeFactura)
                {
                    continue; // Ya existe factura para esta categoría
                }

                // Calcular montos y crear FacturaServicios
                decimal montoTotal = 0;
                var primerServicio = serviciosDelGrupo.First().Servicio!;
                var facturaServicios = new List<FacturaServicio>();

                foreach (var clienteServicio in serviciosDelGrupo)
                {
                    var servicio = clienteServicio.Servicio!;
                    int cantidad = 1;
                    
                    // Para Streaming, obtener la cantidad de suscripciones
                    if (servicio.Categoria == SD.CategoriaStreaming)
                    {
                        cantidad = clienteServicio.Cantidad;
                    }
                    
                    // Calcular monto del servicio
                    var montoServicio = CalcularMontoServicio(servicio, cliente, mesFacturacion, cantidad);
                    
                    montoTotal += montoServicio;

                    // Crear FacturaServicio - guardar el monto total y la cantidad
                    facturaServicios.Add(new FacturaServicio
                    {
                        ServicioId = servicio.Id,
                        Cantidad = cantidad,
                        Monto = montoServicio
                    });
                }

                // Crear la factura consolidada
                // Usar GenerarNumeroFactura para evitar duplicados y obtener el número correcto
                var numeroFactura = GenerarNumeroFactura(cliente, mesFacturacion, categoria);
                
                var factura = new Factura
                {
                    ClienteId = cliente.Id,
                    ServicioId = primerServicio.Id, // Servicio principal para compatibilidad
                    Categoria = categoria,
                    MesFacturacion = mesFacturacion,
                    Numero = numeroFactura,
                    Monto = montoTotal,
                    Estado = SD.EstadoFacturaPendiente,
                    FechaCreacion = DateTime.Now,
                    FacturaServicios = facturaServicios
                };

                facturasACrear.Add(factura);
            }
        }

        // Guardar todas las facturas en una sola transacción
        if (facturasACrear.Any())
        {
            _context.Facturas.AddRange(facturasACrear);
            _context.SaveChanges();
        }
    }

    /// <summary>
    /// Calcula el monto de un servicio
    /// </summary>
    private decimal CalcularMontoServicio(
        Servicio servicio, 
        Cliente cliente, 
        DateTime mesFacturacion, 
        int cantidad)
    {
        if (servicio.Categoria == SD.CategoriaStreaming)
        {
            // Streaming: siempre precio completo (precio * cantidad)
            return servicio.Precio * cantidad;
        }
        else
        {
            // Internet: aplicar proporcional
            // IMPORTANTE: Usar FechaCreacion del cliente para el cálculo proporcional
            var precioTotalSinProporcional = servicio.Precio * cantidad;
            var montoProporcionalUnitario = CalcularMontoProporcionalConFechaInicio(cliente, servicio, mesFacturacion, cliente.FechaCreacion);
            var factorProporcional = servicio.Precio > 0 ? montoProporcionalUnitario / servicio.Precio : 1;
            return precioTotalSinProporcional * factorProporcional;
        }
    }

    /// <summary>
    /// Calcula el monto proporcional considerando la fecha de creación del cliente.
    /// El proporcional se aplica solo si el cliente se creó en el mes de facturación y después del día 5.
    /// El ciclo es del día 5 al día 5 de cada mes.
    /// </summary>
    private decimal CalcularMontoProporcionalConFechaInicio(Cliente cliente, Servicio servicio, DateTime mesFacturacion, DateTime fechaInicioServicio)
    {
        // IMPORTANTE: Usar la fecha de creación del cliente para el cálculo proporcional
        var fechaInicio = cliente.FechaCreacion.Date;
        var primerDiaMesFacturacion = new DateTime(mesFacturacion.Year, mesFacturacion.Month, 1);
        var ultimoDiaMesFacturacion = primerDiaMesFacturacion.AddMonths(1).AddDays(-1);
        
        // Si el cliente inició después del mes de facturación, no debe facturarse aún
        // (esto no debería pasar en la generación automática, pero es una validación de seguridad)
        if (fechaInicio > ultimoDiaMesFacturacion)
        {
            return servicio.Precio; // No debería facturarse, pero por seguridad retornamos precio completo
        }
        
        // Si el cliente inició antes del mes de facturación, ya pagó proporcional en su primera factura
        // Por lo tanto, debe pagar mes completo
        if (fechaInicio < primerDiaMesFacturacion)
        {
            return servicio.Precio;
        }
        
        // Si el cliente inició en el mes de facturación:
        // - Si inició el día 5 o antes, paga mes completo
        if (fechaInicio.Day <= 5)
        {
            return servicio.Precio;
        }
        
        // - Si inició después del día 5, calcular proporcional
        // El ciclo de facturación es del día 5 al día 5 (del 5 de un mes al 5 del siguiente mes = 30 días)
        // Los días facturados se cuentan desde la fecha de inicio hasta el día 5 del mes siguiente
        var ultimoDiaDelMes = new DateTime(mesFacturacion.Year, mesFacturacion.Month, DateTime.DaysInMonth(mesFacturacion.Year, mesFacturacion.Month));
        var mesSiguiente = mesFacturacion.AddMonths(1);
        var dia5MesSiguiente = new DateTime(mesSiguiente.Year, mesSiguiente.Month, 5);
        
        // Calcular días facturados: desde fecha de inicio hasta el día 5 del mes siguiente (incluyendo ambos días)
        // Ejemplo: Cliente entra el 13 nov → cuenta del 13 nov al 5 dic = 23 días
        var diasFacturados = (dia5MesSiguiente.Date - fechaInicio.Date).Days + 1;
        
        if (diasFacturados <= 0)
        {
            return servicio.Precio;
        }

        // El ciclo completo es del día 5 al día 5 = 30 días (del 5 de un mes al 5 del siguiente mes)
        // Ejemplo: del 5 de noviembre al 5 de diciembre = 30 días
        const int diasDelCiclo = 30;
        
        // Calcular costo por día (precio del servicio dividido entre los 30 días del ciclo)
        var costoPorDia = servicio.Precio / diasDelCiclo;
        
        // Calcular monto proporcional: días facturados × costo por día
        var montoProporcional = diasFacturados * costoPorDia;
        
        // Asegurar que el monto proporcional no exceda el precio completo (por seguridad)
        if (montoProporcional > servicio.Precio)
        {
            montoProporcional = servicio.Precio;
        }
        
        // Redondear a 2 decimales
        return Math.Round(montoProporcional, 2);
    }

    public decimal CalcularTotalPendiente()
    {
        return _context.Facturas
            .Where(f => f.Estado == SD.EstadoFacturaPendiente)
            .Sum(f => f.Monto);
    }

    public decimal CalcularTotalPagado()
    {
        return _context.Pagos.Sum(p => p.Monto);
    }

    public int ObtenerTotal()
    {
        return _context.Facturas.Count();
    }

    public int ObtenerTotalPagadas()
    {
        return _context.Facturas.Count(f => f.Estado == SD.EstadoFacturaPagada);
    }

    public int ObtenerTotalPendientes()
    {
        return _context.Facturas.Count(f => f.Estado == SD.EstadoFacturaPendiente);
    }

    public decimal ObtenerMontoTotal()
    {
        return _context.Facturas.Sum(f => f.Monto);
    }
}

