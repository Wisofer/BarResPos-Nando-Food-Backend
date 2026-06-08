using System;
using System.IO;

namespace BarRestPOS.Utils;

public static class BackupHelper
{
    public static void CrearRespaldo(string tipoEvento)
    {
        try
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BarRestPOS");
            string dbFile = Path.Combine(appDataFolder, "barrestpos.db");
            
            if (!File.Exists(dbFile))
            {
                dbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "barrestpos.db");
                if (!File.Exists(dbFile))
                {
                    dbFile = "barrestpos.db";
                    if (!File.Exists(dbFile))
                    {
                        return;
                    }
                }
            }

            // Crear carpeta destino en CommonApplicationData para los respaldos
            string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BarResPos_Backups");
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Generar nombre de archivo único con fecha, hora y el tipo de evento (inicio/cierre)
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string backupFileName = $"respaldo_{timestamp}_{tipoEvento}.db";
            string targetPath = Path.Combine(targetDir, backupFileName);

            // Copiar el archivo físicamente sobreescribiendo si ya existe alguno idéntico
            File.Copy(dbFile, targetPath, true);
        }
        catch (Exception ex)
        {
            // No relanzar — el sistema principal NUNCA debe detenerse por fallos de respaldo.
            // El error se escribe en consola para depuración (visible en desarrollo / logs de systemd).
            Console.Error.WriteLine($"[BackupHelper] Error al crear respaldo ({tipoEvento}): {ex.Message}");
        }
    }
}
