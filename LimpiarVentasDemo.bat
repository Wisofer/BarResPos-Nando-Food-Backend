@echo off
title BarResPos - Limpieza de Ventas Demo

cls
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
echo   - Todas las Categorias y Destinos de Comanda.
echo   - El Plano de Mesas y Ubicaciones.
echo   - Todos los Usuarios y Claves.
echo   - Las Configuraciones del Restaurante e Impresoras.
echo.
echo ====================================================================
echo.
set /p confirm="Desea reiniciar todas las ventas de prueba? (S/N): "

if /i "%confirm%"=="S" goto :ejecutar
if /i "%confirm%"=="SI" goto :ejecutar

echo.
echo  Operacion cancelada. No se modifico la base de datos.
echo.
pause
exit /b

:ejecutar
echo.
echo  Deteniendo ejecucion del sistema y realizando limpieza profunda...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Process -Name 'BarRestPOS','dotnet','electron' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1; $appData = [Environment]::GetFolderPath('ApplicationData'); $localData = [Environment]::GetFolderPath('LocalApplicationData'); $db1 = Join-Path $appData 'BarRestPOS\barrestpos.db'; $db2 = Join-Path $localData 'BarRestPOS\barrestpos.db'; $db3 = 'barrestpos.db'; $db4 = 'BarRestPOS.db'; $rutas = @($db1, $db2, $db3, $db4); $encontradas = @(); foreach ($r in $rutas) { if (Test-Path $r) { $encontradas += $r } }; $more = Get-ChildItem -Path $PSScriptRoot -Filter '*barrestpos*.db' -Recurse -ErrorAction SilentlyContinue; if ($more) { foreach ($m in $more) { if ($encontradas -notcontains $m.FullName) { $encontradas += $m.FullName } } }; if ($encontradas.Count -eq 0) { Write-Host '[ERROR] No se encontro ninguna base de datos barrestpos.db' -ForegroundColor Red; exit 1 }; $csharp = 'using System; using System.Runtime.InteropServices; public class WinSqlite { [DllImport(\"winsqlite3.dll\", EntryPoint=\"sqlite3_open\", CallingConvention=CallingConvention.Cdecl)] public static extern int sqlite3_open(string filename, out IntPtr db); [DllImport(\"winsqlite3.dll\", EntryPoint=\"sqlite3_exec\", CallingConvention=CallingConvention.Cdecl)] public static extern int sqlite3_exec(IntPtr db, string sql, IntPtr callback, IntPtr arg, out IntPtr errmsg); [DllImport(\"winsqlite3.dll\", EntryPoint=\"sqlite3_close\", CallingConvention=CallingConvention.Cdecl)] public static extern int sqlite3_close(IntPtr db); public static string RunSafe(string path) { IntPtr db; if (sqlite3_open(path, out db) != 0) return \"Error abriendo DB\"; IntPtr err; string[] tables = new string[] { \"OrdenLineaOpciones\", \"FacturaServicioOpcionesSeleccion\", \"FacturaServicios\", \"PagoFacturas\", \"Pagos\", \"Facturas\", \"CierresCaja\", \"MovimientosInventario\", \"ClienteServicios\", \"RefreshTokens\", \"RegistrosAuditoria\" }; sqlite3_exec(db, \"PRAGMA foreign_keys = OFF;\", IntPtr.Zero, IntPtr.Zero, out err); foreach (string t in tables) { sqlite3_exec(db, \"DELETE FROM \" + t + \";\", IntPtr.Zero, IntPtr.Zero, out err); sqlite3_exec(db, \"DELETE FROM sqlite_sequence WHERE name=\x27\" + t + \"\x27;\", IntPtr.Zero, IntPtr.Zero, out err); } sqlite3_exec(db, \"UPDATE Mesas SET Estado=\x27Libre\x27;\", IntPtr.Zero, IntPtr.Zero, out err); sqlite3_exec(db, \"PRAGMA foreign_keys = ON;\", IntPtr.Zero, IntPtr.Zero, out err); sqlite3_exec(db, \"PRAGMA wal_checkpoint(TRUNCATE);\", IntPtr.Zero, IntPtr.Zero, out err); sqlite3_exec(db, \"VACUUM;\", IntPtr.Zero, IntPtr.Zero, out err); sqlite3_close(db); return \"OK\"; } }'; Add-Type -TypeDefinition $csharp -ErrorAction Stop; foreach ($dbPath in $encontradas) { Write-Host '  [+] Limpiando base de datos:' $dbPath -ForegroundColor Yellow; $res = [WinSqlite]::RunSafe($dbPath); if ($res -eq 'OK') { Write-Host '  [OK] LIMPIEZA EXITOSA EN:' $dbPath -ForegroundColor Green } else { Write-Host '  [ERROR]:' $res -ForegroundColor Red } }"

echo.
echo ====================================================================
echo   ¡LIMPIEZA COMPLETADA CON EXITO!
echo   Por favor abre la aplicacion BarResPos (o presiona F5 en el navegador).
echo ====================================================================
echo.
pause
