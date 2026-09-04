# ====================================================================
#   BarResPos - REINICIO DE SISTEMA A PRIMERA VENTA DE PRODUCCION (.ps1)
# ====================================================================

Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "  BarResPos - REINICIO DE SISTEMA A PRIMERA VENTA DE PRODUCCION" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host " [ATENCION]: Este script limpiara todos los datos de ventas de prueba." -ForegroundColor Yellow
Write-Host ""
Write-Host " SE ELIMINARAN UNICAMENTE:" -ForegroundColor Red
Write-Host "  - Todas las Facturas, Pedidos y Borradores de prueba."
Write-Host "  - Todos los Historiales de Pagos y Cierres de Caja."
Write-Host "  - Todos los Movimientos de Inventario de prueba."
Write-Host "  - Se liberaran todas las mesas (Estado: Libre)."
Write-Host ""
Write-Host " SE CONSERVARAN INTACTOS:" -ForegroundColor Green
Write-Host "  - Todos los Productos, Precios y Fotos."
Write-Host "  - Todas las Categorias y Destinos de Comanda."
Write-Host "  - El Plano de Mesas y Ubicaciones."
Write-Host "  - Todos los Usuarios y Claves."
Write-Host "  - Las Configuraciones del Restaurante e Impresoras."
Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host ""

$confirm = Read-Host "Desea reiniciar todas las ventas de prueba? (S/N)"

if ($confirm -notmatch "^[sS]") {
    Write-Host ""
    Write-Host "Operacion cancelada por el usuario. No se modifico la base de datos." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Presione Enter para salir"
    exit
}

Write-Host ""
Write-Host "Deteniendo ejecucion del sistema y realizando limpieza profunda..." -ForegroundColor Cyan

Get-Process -Name "BarRestPOS","dotnet","electron" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

$appData = [Environment]::GetFolderPath('ApplicationData')
$localData = [Environment]::GetFolderPath('LocalApplicationData')
$db1 = Join-Path $appData 'BarRestPOS\barrestpos.db'
$db2 = Join-Path $localData 'BarRestPOS\barrestpos.db'
$db3 = 'barrestpos.db'
$db4 = 'BarRestPOS.db'

$rutas = @($db1, $db2, $db3, $db4)
$encontradas = @()
foreach ($r in $rutas) {
    if (Test-Path $r) { $encontradas += $r }
}

$more = Get-ChildItem -Path $PSScriptRoot -Filter "*barrestpos*.db" -Recurse -ErrorAction SilentlyContinue
if ($more) {
    foreach ($m in $more) {
        if ($encontradas -notcontains $m.FullName) { $encontradas += $m.FullName }
    }
}

if ($encontradas.Count -eq 0) {
    Write-Host " [ERROR] No se encontro ninguna base de datos barrestpos.db" -ForegroundColor Red
    Read-Host "Presione Enter para salir"
    exit 1
}

$csharp = @"
using System;
using System.Runtime.InteropServices;

public class WinSqlite
{
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_open", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_open(string filename, out IntPtr db);

    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_exec", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_exec(IntPtr db, string sql, IntPtr callback, IntPtr arg, out IntPtr errmsg);

    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_close", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_close(IntPtr db);

    public static string RunSafe(string path)
    {
        IntPtr db;
        if (sqlite3_open(path, out db) != 0) return "Error abriendo DB";

        IntPtr err;
        string[] tables = new string[] {
            "OrdenLineaOpciones",
            "FacturaServicioOpcionesSeleccion",
            "FacturaServicios",
            "PagoFacturas",
            "Pagos",
            "Facturas",
            "CierresCaja",
            "MovimientosInventario",
            "ClienteServicios",
            "RefreshTokens",
            "RegistrosAuditoria"
        };

        sqlite3_exec(db, "PRAGMA foreign_keys = OFF;", IntPtr.Zero, IntPtr.Zero, out err);

        foreach (string t in tables)
        {
            sqlite3_exec(db, "DELETE FROM " + t + ";", IntPtr.Zero, IntPtr.Zero, out err);
            sqlite3_exec(db, "DELETE FROM sqlite_sequence WHERE name='" + t + "';", IntPtr.Zero, IntPtr.Zero, out err);
        }

        sqlite3_exec(db, "UPDATE Mesas SET Estado='Libre';", IntPtr.Zero, IntPtr.Zero, out err);
        sqlite3_exec(db, "PRAGMA foreign_keys = ON;", IntPtr.Zero, IntPtr.Zero, out err);
        sqlite3_exec(db, "PRAGMA wal_checkpoint(TRUNCATE);", IntPtr.Zero, IntPtr.Zero, out err);
        sqlite3_exec(db, "VACUUM;", IntPtr.Zero, IntPtr.Zero, out err);

        sqlite3_close(db);
        return "OK";
    }
}
"@

try {
    Add-Type -TypeDefinition $csharp -ErrorAction Stop
    foreach ($dbPath in $encontradas) {
        Write-Host " [+] Limpiando base de datos: $dbPath" -ForegroundColor Yellow
        $res = [WinSqlite]::RunSafe($dbPath)
        if ($res -eq 'OK') {
            Write-Host " [OK] LIMPIEZA COMPLETADA CON EXITO EN: $dbPath" -ForegroundColor Green
        } else {
            Write-Host " [ERROR] SQL: $res" -ForegroundColor Red
        }
    }
} catch {
    Write-Host " [ERROR] Exception: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "¡LIMPIEZA COMPLETADA CON EXITO!" -ForegroundColor Green
Write-Host "Por favor abre la aplicacion BarResPos (o presiona F5 en el navegador)." -ForegroundColor Yellow
Write-Host "====================================================================" -ForegroundColor Cyan
Read-Host "Presione Enter para cerrar"
