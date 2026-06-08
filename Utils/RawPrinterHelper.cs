using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BarRestPOS.Utils;

/// <summary>
/// Permite enviar un arreglo de bytes (comandos RAW / ESC/POS) directamente a la cola
/// de una impresora de Windows, esquivando el sistema gráfico.
/// </summary>
public static class RawPrinterHelper
{
    // Estructuras de Win32 API
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string pDocName = string.Empty;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string pDataType = string.Empty;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    /// <summary>
    /// Envía un arreglo de bytes RAW a la impresora de Windows.
    /// </summary>
    public static bool SendBytesToPrinter(string szPrinterName, byte[] data, string docName = "TicketPOS")
    {
        if (data == null || data.Length == 0) return false;
        
        IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(data.Length);
        Marshal.Copy(data, 0, pUnmanagedBytes, data.Length);
        
        bool success = SendBytesToPrinter(szPrinterName, pUnmanagedBytes, data.Length, docName);
        
        Marshal.FreeCoTaskMem(pUnmanagedBytes);
        return success;
    }

    private static bool SendBytesToPrinter(string szPrinterName, IntPtr pBytes, int dwCount, string docName)
    {
        int dwError = 0, dwWritten = 0;
        IntPtr hPrinter = IntPtr.Zero;
        DOCINFOA di = new DOCINFOA();
        bool success = false;

        di.pDocName = docName;
        di.pDataType = "RAW";

        if (OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero))
        {
            if (StartDocPrinter(hPrinter, 1, di))
            {
                if (StartPagePrinter(hPrinter))
                {
                    success = WritePrinter(hPrinter, pBytes, dwCount, out dwWritten);
                    EndPagePrinter(hPrinter);
                }
                EndDocPrinter(hPrinter);
            }
            ClosePrinter(hPrinter);
        }

        if (!success)
        {
            dwError = Marshal.GetLastWin32Error();
            if (dwError != 0)
            {
                Debug.WriteLine($"Error RAW Printing a '{szPrinterName}'. Win32Error: {dwError}");
            }
        }

        return success;
    }
}
