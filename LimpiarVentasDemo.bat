@echo off
chcp 65001 > nul
title BarResPos - Herramienta de Limpieza de Ventas Demo para Produccion

echo ====================================================================
echo   BarResPos - REINICIO DE SISTEMA A PRIMERA VENTA DE PRODUCCION
echo ====================================================================
echo.
echo  [ATENCION]: Este script limpiara todos los datos de ventas de prueba.
echo.
echo  SE ELIMINARAN UNICAMENTE:
echo   - Todas las Facturas, Pedidos y Borradores de prueba.
echo   - Todos los Historiales de Pagos y Cierres de Caja.
echo   - Todos los Movimientos de Inventario de prueba.
echo   - Se liberaran todas las mesas (Estado: Libre).
echo.
echo  SE CONSERVARAN INTACTOS:
echo   - Todos los Productos, Precios y Fotos.
echo   - Todas las Categorias y Destinos de Comanda (Cocina, Bar, Solo Cobro).
echo   - El Plano de Mesas y Ubicaciones.
echo   - Todos los Usuarios y Claves.
echo   - Las Configuraciones del Restaurante e Impresoras.
echo.
echo ====================================================================
echo.
set /p confirm="¿Esta seguro que desea reiniciar todas las ventas? (S/N): "

if /i "%confirm%" NEQ "S" (
    echo.
    echo  Operacion cancelada por el usuario. No se realizaron cambios.
    echo.
    pause
    exit /b
)

echo.
echo  Buscando base de datos SQLite...

set DB_PATH=barrestpos.db

if not exist "%DB_PATH%" (
    if exist "BarRestPOS.db" (
        set DB_PATH=BarRestPOS.db
    ) else (
        if exist "%LOCALAPPDATA%\BarRestPOS\barrestpos.db" (
            set DB_PATH=%LOCALAPPDATA%\BarRestPOS\barrestpos.db
        )
    )
)

if not exist "%DB_PATH%" (
    echo.
    echo  [ERROR]: No se encontro el archivo de base de datos (%DB_PATH%).
    echo  Por favor ejecuta este script en la carpeta donde esta el sistema backend.
    echo.
    pause
    exit /b
)

echo  Base de datos encontrada en: %DB_PATH%
echo  Ejecutando limpieza de tablas de ventas...
echo.

powershell -NoProfile -Command ^
    "$db = '%DB_PATH%';" ^
    "$query = \" " ^
    "  PRAGMA foreign_keys = OFF; " ^
    "  DELETE FROM FacturaServicioOpcionesSeleccion; " ^
    "  DELETE FROM FacturaServicios; " ^
    "  DELETE FROM PagoFacturas; " ^
    "  DELETE FROM Pagos; " ^
    "  DELETE FROM Facturas; " ^
    "  DELETE FROM CierresCaja; " ^
    "  DELETE FROM MovimientosInventario; " ^
    "  DELETE FROM RegistrosAuditoria; " ^
    "  UPDATE Mesas SET Estado = 'Libre'; " ^
    "  DELETE FROM sqlite_sequence WHERE name IN ('Facturas', 'FacturaServicios', 'Pagos', 'PagoFacturas', 'CierresCaja', 'MovimientosInventario', 'RegistrosAuditoria', 'FacturaServicioOpcionesSeleccion'); " ^
    "  PRAGMA foreign_keys = ON; " ^
    "  VACUUM; " ^
    "\"; " ^
    "try { " ^
    "  $conn = New-Object System.Data.SQLite.SQLiteConnection(\"Data Source=$db;Version=3;\"); " ^
    "  $conn.Open(); " ^
    "  $cmd = $conn.CreateCommand(); " ^
    "  $cmd.CommandText = $query; " ^
    "  $cmd.ExecuteNonQuery(); " ^
    "  $conn.Close(); " ^
    "  Write-Host '  [OK] Limpieza ejecutada exitosamente mediante SQLite ADO.NET Driver.' -ForegroundColor Green; " ^
    "} catch { " ^
    "  try { " ^
    "    Add-Type -Assembly 'System.Data'; " ^
    "    $connStr = \"Data Source=$db\"; " ^
    "    $conn = New-Object -TypeName Microsoft.Data.Sqlite.SqliteConnection -ArgumentList $connStr; " ^
    "    $conn.Open(); " ^
    "    $cmd = $conn.CreateCommand(); " ^
    "    $cmd.CommandText = $query; " ^
    "    $cmd.ExecuteNonQuery(); " ^
    "    $conn.Close(); " ^
    "    Write-Host '  [OK] Limpieza ejecutada exitosamente mediante Microsoft.Data.Sqlite.' -ForegroundColor Green; " ^
    "  } catch { " ^
    "    $psExec = \"& sqlite3 '$db' '$query'\"; " ^
    "    Invoke-Expression $psExec; " ^
    "    Write-Host '  [OK] Limpieza ejecutada mediante cliente SQLite.' -ForegroundColor Green; " ^
    "  } " ^
    "}"

echo.
echo ====================================================================
echo   ¡SISTEMA REINICIADO Y LISTO PARA LA PRIMERA VENTA EN PRODUCCION!
echo ====================================================================
echo.
pause
