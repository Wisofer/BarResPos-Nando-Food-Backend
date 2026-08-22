-- ====================================================================
--   BarResPos - SCRIPT SQL DE REINICIO DE VENTAS PARA PRODUCCIÓN
-- ====================================================================
-- Instrucciones: Ejecutar este script sobre barrestpos.db para dejar 
-- el sistema limpio como si fuera su primer día de ventas en producción.
-- ====================================================================

PRAGMA foreign_keys = OFF;

-- 1. Eliminar transacciones y facturación de prueba
DELETE FROM FacturaServicioOpcionesSeleccion;
DELETE FROM FacturaServicios;
DELETE FROM PagoFacturas;
DELETE FROM Pagos;
DELETE FROM Facturas;
DELETE FROM CierresCaja;
DELETE FROM MovimientosInventario;
DELETE FROM RegistrosAuditoria;

-- 2. Restablecer estado de todas las mesas a 'Libre'
UPDATE Mesas SET Estado = 'Libre';

-- 3. Reiniciar autoincrementables de SQLite para que la primera factura sea la #1
DELETE FROM sqlite_sequence WHERE name IN (
    'Facturas', 
    'FacturaServicios', 
    'Pagos', 
    'PagoFacturas', 
    'CierresCaja', 
    'MovimientosInventario', 
    'RegistrosAuditoria', 
    'FacturaServicioOpcionesSeleccion'
);

PRAGMA foreign_keys = ON;

-- 4. Compactar la base de datos para recuperar espacio en disco
VACUUM;

-- FIN DE LIMPIEZA: Productos, Precios, Categorías, Usuarios y Mesas intactos.
