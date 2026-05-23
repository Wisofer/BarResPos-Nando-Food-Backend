using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.Reflection;

namespace BarRestPOS.Services;

/// <summary>
/// Servicio para exportar datos a Excel
/// </summary>
public class ExcelExportService
{
    private static readonly Color HeaderIndigo = Color.FromArgb(79, 70, 229);
    private static readonly Color HeaderGreen = Color.FromArgb(16, 185, 129);
    private static readonly Color HeaderBlue = Color.FromArgb(59, 130, 246);

    public ExcelExportService()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    private ExcelWorksheet PrepareSheet(ExcelPackage package, string name, string[] headers, Color headerColor)
    {
        var worksheet = package.Workbook.Worksheets.Add(name);
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cells[1, i + 1].Value = headers[i];
        }

        using (var range = worksheet.Cells[1, 1, 1, headers.Length])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(headerColor);
            range.Style.Font.Color.SetColor(Color.White);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
        return worksheet;
    }

    public byte[] ExportarPedidos(IEnumerable<dynamic> pedidos)
    {
        using var package = new ExcelPackage();
        string[] headers = {
            "Número de Pedido", "Fecha", "Tipo", "Mesa", "Mesero", "Estado",
            "Monto consumo (C$)", "Fecha Pago", "Descuento cobro (C$)", "Neto cobrado (C$)"
        };
        var worksheet = PrepareSheet(package, "Pedidos", headers, HeaderIndigo);

        int row = 2;
        foreach (var pedido in pedidos)
        {
            worksheet.Cells[row, 1].Value = pedido.Numero ?? "";
            worksheet.Cells[row, 2].Value = pedido.FechaCreacion?.ToString("dd/MM/yyyy HH:mm") ?? "";
            worksheet.Cells[row, 3].Value = pedido.Tipo ?? "-";
            worksheet.Cells[row, 4].Value = pedido.Mesa ?? "-";
            worksheet.Cells[row, 5].Value = pedido.Mesero ?? "-";
            worksheet.Cells[row, 6].Value = pedido.Estado ?? "";
            
            if (pedido.Monto != null)
            {
                worksheet.Cells[row, 7].Value = Convert.ToDecimal(pedido.Monto);
                worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
            }
            
            worksheet.Cells[row, 8].Value = pedido.FechaPagado?.ToString("dd/MM/yyyy HH:mm") ?? "-";

            decimal? descOpt = TryGetNullableDecimal((object)pedido, "DescuentoCordobas");
            if (descOpt.HasValue)
            {
                worksheet.Cells[row, 9].Value = descOpt.Value;
                worksheet.Cells[row, 9].Style.Numberformat.Format = "#,##0.00";
            }
            else worksheet.Cells[row, 9].Value = "-";

            decimal? netoOpt = TryGetNullableDecimal((object)pedido, "TotalNetoCobradoCordobas");
            if (netoOpt.HasValue)
            {
                worksheet.Cells[row, 10].Value = netoOpt.Value;
                worksheet.Cells[row, 10].Style.Numberformat.Format = "#,##0.00";
            }
            else worksheet.Cells[row, 10].Value = "-";

            row++;
        }

        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Reporte de Pedidos");
        return package.GetAsByteArray();
    }

    public byte[] ExportarOrdenesVentasReporte(IEnumerable<dynamic> ordenes)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Número/Código", "Fecha", "Origen", "Referencia", "Mesero", "Método Pago", "Monto (C$)" };
        var worksheet = PrepareSheet(package, "Órdenes Ventas", headers, HeaderIndigo);

        int row = 2;
        foreach (var orden in ordenes)
        {
            worksheet.Cells[row, 1].Value = orden.numero ?? orden.Numero ?? "";
            worksheet.Cells[row, 2].Value = (orden.fecha ?? orden.Fecha) is DateTime dt ? dt.ToString("dd/MM/yyyy HH:mm") : "";
            worksheet.Cells[row, 3].Value = orden.origen ?? "";
            worksheet.Cells[row, 4].Value = orden.referencia ?? "";
            worksheet.Cells[row, 5].Value = orden.mesero ?? "";
            worksheet.Cells[row, 6].Value = orden.metodoPago ?? "S/E";

            var m = orden.monto ?? orden.Monto;
            if (m != null)
            {
                worksheet.Cells[row, 7].Value = Convert.ToDecimal(m);
                worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
            }
            row++;
        }

        if (row > 2) AddTotalRow(worksheet, 2, row - 1, new[] { 7 });
        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Reporte de Ventas por Órdenes");
        return package.GetAsByteArray();
    }

    public byte[] ExportarProductos(IEnumerable<dynamic> productos)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Código", "Nombre", "Categoría", "Proveedor", "Precio Compra", "Precio Venta", "Stock", "Stock Mínimo", "Controla Stock", "Estado" };
        var worksheet = PrepareSheet(package, "Productos", headers, HeaderGreen);

        int row = 2;
        foreach (var p in productos)
        {
            worksheet.Cells[row, 1].Value = GetValueSafe(p, "Codigo")?.ToString() ?? "";
            worksheet.Cells[row, 2].Value = GetValueSafe(p, "Nombre")?.ToString() ?? "";
            worksheet.Cells[row, 3].Value = GetValueSafe(p, "Categoria")?.ToString() ?? "";
            worksheet.Cells[row, 4].Value = GetValueSafe(p, "Proveedor")?.ToString() ?? "";
            
            var pc = GetValueSafe(p, "PrecioCompra");
            if (pc != null) { worksheet.Cells[row, 5].Value = Convert.ToDecimal(pc); worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00"; }

            var pv = GetValueSafe(p, "Precio") ?? GetValueSafe(p, "PrecioVenta");
            if (pv != null) { worksheet.Cells[row, 6].Value = Convert.ToDecimal(pv); worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00"; }
            
            worksheet.Cells[row, 7].Value = GetValueSafe(p, "Stock") ?? 0;
            worksheet.Cells[row, 8].Value = GetValueSafe(p, "StockMinimo") ?? 0;
            worksheet.Cells[row, 9].Value = (GetValueSafe(p, "ControlarStock") as bool? == true) ? "Sí" : "No";
            worksheet.Cells[row, 10].Value = (GetValueSafe(p, "Activo") as bool? == true) ? "Activo" : "Inactivo";
            row++;
        }

        if (row == 2) { worksheet.Cells[2, 1].Value = "No hay productos para mostrar"; worksheet.Cells[2, 1, 2, 10].Merge = true; worksheet.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center; }
        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Listado de Productos");
        return package.GetAsByteArray();
    }

    public byte[] ExportarMovimientosInventario(IEnumerable<dynamic> movimientos)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Fecha", "Producto", "Tipo", "Subtipo", "Cantidad", "Stock Anterior", "Stock Nuevo", "Usuario", "Observaciones" };
        var worksheet = PrepareSheet(package, "Movimientos Inventario", headers, HeaderBlue);

        int row = 2;
        foreach (var mov in movimientos)
        {
            worksheet.Cells[row, 1].Value = mov.Fecha?.ToString("dd/MM/yyyy HH:mm") ?? "";
            worksheet.Cells[row, 2].Value = mov.Producto ?? "";
            worksheet.Cells[row, 3].Value = mov.Tipo ?? "";
            worksheet.Cells[row, 4].Value = mov.Subtipo ?? "";
            worksheet.Cells[row, 5].Value = mov.Cantidad ?? 0;
            worksheet.Cells[row, 6].Value = mov.StockAnterior ?? 0;
            worksheet.Cells[row, 7].Value = mov.StockNuevo ?? 0;
            worksheet.Cells[row, 8].Value = mov.Usuario ?? "";
            worksheet.Cells[row, 9].Value = mov.Observaciones ?? "";
            row++;
        }

        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Movimientos de Inventario");
        return package.GetAsByteArray();
    }

    public byte[] ExportarVentasPorMesero(IEnumerable<dynamic> items)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Mesero", "Órdenes", "Total ventas", "Promedio Ticket" };
        var worksheet = PrepareSheet(package, "Ventas por Mesero", headers, HeaderIndigo);

        int row = 2;
        foreach (var item in items)
        {
            worksheet.Cells[row, 1].Value = item.mesero ?? "";
            worksheet.Cells[row, 2].Value = item.ordenes ?? 0;
            SetCellMoney(worksheet, row, 3, item.totalVentas);
            SetCellMoney(worksheet, row, 4, item.promedioTicket);
            row++;
        }

        if (row > 2) AddTotalRow(worksheet, 2, row - 1, new[] { 2, 3 });
        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Desempeño de Meseros");
        return package.GetAsByteArray();
    }

    public byte[] ExportarVentasPorCategoria(IEnumerable<dynamic> items)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Categoría", "Cantidad Vendida", "Total ventas (C$)" };
        var worksheet = PrepareSheet(package, "Ventas por Categoría", headers, HeaderIndigo);

        int row = 2;
        foreach (var item in items)
        {
            worksheet.Cells[row, 1].Value = item.Categoria ?? "";
            worksheet.Cells[row, 2].Value = item.Cantidad ?? 0;
            SetCellMoney(worksheet, row, 3, item.Monto);
            row++;
        }

        if (row > 2) AddTotalRow(worksheet, 2, row - 1, new[] { 2, 3 });
        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Ventas por Categoría");
        return package.GetAsByteArray();
    }

    public byte[] ExportarHistorialCierres(IEnumerable<dynamic> items)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Cierre #", "Fecha", "Estado", "Monto Inicial", "Ventas Totales", "Inyección/Retiros", "Monto Esperado", "Monto Real", "Diferencia", "Usuario" };
        var worksheet = PrepareSheet(package, "Cierres de Caja", headers, HeaderIndigo);

        int row = 2;
        foreach (var item in items)
        {
            worksheet.Cells[row, 1].Value = item.id;
            worksheet.Cells[row, 2].Value = ((DateTime)item.fecha).ToString("dd/MM/yyyy HH:mm");
            worksheet.Cells[row, 3].Value = item.estado ?? "";
            SetCellMoney(worksheet, row, 4, item.montoInicial);
            SetCellMoney(worksheet, row, 5, item.totalVentas);
            decimal inyRet = Convert.ToDecimal(item.montoEsperado ?? 0m) - Convert.ToDecimal(item.montoInicial ?? 0m) - Convert.ToDecimal(item.totalVentas ?? 0m);
            SetCellMoney(worksheet, row, 6, inyRet);
            SetCellMoney(worksheet, row, 7, item.montoEsperado);
            SetCellMoney(worksheet, row, 8, item.montoReal);
            SetCellMoney(worksheet, row, 9, item.diferencia);
            
            if (Convert.ToDecimal(item.diferencia ?? 0m) < 0) { worksheet.Cells[row, 9].Style.Font.Color.SetColor(Color.Red); worksheet.Cells[row, 9].Style.Font.Bold = true; }
            worksheet.Cells[row, 10].Value = item.usuario ?? "";
            row++;
        }

        if (row > 2) AddTotalRow(worksheet, 2, row - 1, new[] { 4, 5, 6, 7, 8, 9 });
        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Historial de Cierres de Caja");
        return package.GetAsByteArray();
    }

    public byte[] ExportarVentasReporte(IEnumerable<dynamic> ventas)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Número", "Fecha", "Estado", "Origen", "Referencia", "Método", "Moneda", "Líneas", "Subtotal Líneas", "Total Neto" };
        var worksheet = PrepareSheet(package, "Ventas", headers, HeaderIndigo);

        int row = 2;
        foreach (var v in ventas)
        {
            worksheet.Cells[row, 1].Value = v.numero ?? "";
            worksheet.Cells[row, 2].Value = (v.fecha is DateTime dt) ? dt.ToString("dd/MM/yyyy HH:mm") : "";
            worksheet.Cells[row, 3].Value = v.estado ?? "";
            worksheet.Cells[row, 4].Value = v.origen ?? "";
            worksheet.Cells[row, 5].Value = v.referencia ?? "";
            worksheet.Cells[row, 6].Value = v.metodoPago ?? "";
            worksheet.Cells[row, 7].Value = v.moneda ?? "";
            worksheet.Cells[row, 8].Value = v.lineas ?? 0;
            SetCellMoney(worksheet, row, 9, v.subtotalLineas);
            SetCellMoney(worksheet, row, 10, v.total);
            row++;
        }

        if (row > 2) AddTotalRow(worksheet, 2, row - 1, new[] { 8, 9, 10 });
        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Reporte de Ventas");
        return package.GetAsByteArray();
    }

    public byte[] ExportarVentasPorCategoriaConDesglose(IEnumerable<dynamic> items)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Categoría", "Cantidad", "Monto (C$)" };
        var resumen = PrepareSheet(package, "Resumen Categorías", headers, HeaderIndigo);

        int row = 2;
        foreach (var item in items)
        {
            resumen.Cells[row, 1].Value = item.Categoria ?? "";
            resumen.Cells[row, 2].Value = item.Cantidad ?? 0;
            SetCellMoney(resumen, row, 3, item.Monto);
            row++;
        }
        if (row > 2) AddTotalRow(resumen, 2, row - 1, new[] { 2, 3 });
        ApplyExpertStyles(resumen, resumen.Dimension.End.Row, headers.Length, "Ventas por Categoría");

        var detalle = package.Workbook.Worksheets.Add("Detalle Productos");
        detalle.Cells[1, 1].Value = "Categoría";
        detalle.Cells[1, 2].Value = "Código";
        detalle.Cells[1, 3].Value = "Producto";
        detalle.Cells[1, 4].Value = "Cantidad";
        detalle.Cells[1, 5].Value = "Monto (C$)";
        using (var range = detalle.Cells[1, 1, 1, 5])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(HeaderIndigo);
            range.Style.Font.Color.SetColor(Color.White);
        }

        int drow = 2;
        foreach (var c in items)
        {
            foreach (var p in c.Productos)
            {
                detalle.Cells[drow, 1].Value = c.Categoria ?? "";
                detalle.Cells[drow, 2].Value = p.CodigoProducto ?? "";
                detalle.Cells[drow, 3].Value = p.NombreProducto ?? "";
                detalle.Cells[drow, 4].Value = p.Cantidad ?? 0;
                SetCellMoney(detalle, drow, 5, p.Monto);
                drow++;
            }
        }
        if (drow > 2) AddTotalRow(detalle, 2, drow - 1, new[] { 4, 5 });
        ApplyExpertStyles(detalle, detalle.Dimension.End.Row, 5, "Detalle por Producto");
        return package.GetAsByteArray();
    }

    public byte[] ExportarTopProductos(IEnumerable<dynamic> items)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Producto", "Categoría", "Cantidad", "Venta (C$)" };
        var resumen = PrepareSheet(package, "Top Productos", headers, HeaderIndigo);

        int row = 2;
        foreach (var item in items)
        {
            resumen.Cells[row, 1].Value = item.Producto ?? "";
            resumen.Cells[row, 2].Value = item.Categoria ?? "";
            resumen.Cells[row, 3].Value = item.Cantidad ?? 0;
            SetCellMoney(resumen, row, 4, item.Venta);
            row++;
        }
        if (row > 2) AddTotalRow(resumen, 2, row - 1, new[] { 3, 4 });
        ApplyExpertStyles(resumen, resumen.Dimension.End.Row, headers.Length, "Top Productos");

        var desglose = package.Workbook.Worksheets.Add("Desglose Forma Pago");
        desglose.Cells[1, 1].Value = "Producto";
        desglose.Cells[1, 2].Value = "Método";
        desglose.Cells[1, 3].Value = "Moneda";
        desglose.Cells[1, 4].Value = "Unidades";
        desglose.Cells[1, 5].Value = "Monto (C$)";
        using (var range = desglose.Cells[1, 1, 1, 5])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(HeaderIndigo);
            range.Style.Font.Color.SetColor(Color.White);
        }

        int drow = 2;
        foreach (var item in items)
        {
            foreach (var d in item.DesglosePorFormaPago)
            {
                desglose.Cells[drow, 1].Value = item.Producto ?? "";
                desglose.Cells[drow, 2].Value = d.MetodoPago ?? "";
                desglose.Cells[drow, 3].Value = d.Moneda ?? "";
                desglose.Cells[drow, 4].Value = d.CantidadUnidades ?? 0;
                SetCellMoney(desglose, drow, 5, d.MontoCordobas);
                drow++;
            }
        }
        if (drow > 2) AddTotalRow(desglose, 2, drow - 1, new[] { 4, 5 });
        ApplyExpertStyles(desglose, desglose.Dimension.End.Row, 5, "Desglose Forma de Pago");
        return package.GetAsByteArray();
    }

    private void SetCellMoney(ExcelWorksheet sheet, int row, int col, object? val)
    {
        sheet.Cells[row, col].Value = Convert.ToDecimal(val ?? 0m);
        sheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
    }

    private static object? GetValueSafe(object item, string propName)
    {
        if (item == null) return null;
        try {
            var prop = item.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null) return prop.GetValue(item);
            return ((dynamic)item).GetType().GetProperty(propName)?.GetValue(item, null);
        } catch { return null; }
    }

    private static decimal? TryGetNullableDecimal(object item, string propertyName)
    {
        var v = GetValueSafe(item, propertyName);
        return v != null ? Convert.ToDecimal(v) : null;
    }

    private void ApplyExpertStyles(ExcelWorksheet worksheet, int lastRow, int lastCol, string title)
    {
        var range = worksheet.Cells[1, 1, lastRow, lastCol];
        range.Style.Border.Top.Style = range.Style.Border.Bottom.Style = range.Style.Border.Left.Style = range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
        worksheet.Cells.AutoFitColumns();
        for (int i = 1; i <= lastCol; i++) worksheet.Column(i).Width += 2;
    }

    private void AddTotalRow(ExcelWorksheet worksheet, int startRow, int endRow, int[] sumCols)
    {
        int totalRow = endRow + 1;
        worksheet.Cells[totalRow, 1].Value = "TOTALES / RESUMEN";
        worksheet.Cells[totalRow, 1].Style.Font.Bold = true;
        foreach (int col in sumCols) {
            decimal total = 0;
            for (int r = startRow; r <= endRow; r++) {
                if (decimal.TryParse(worksheet.Cells[r, col].Value?.ToString(), out decimal d)) total += d;
            }
            worksheet.Cells[totalRow, col].Value = total;
            worksheet.Cells[totalRow, col].Style.Font.Bold = true;
            worksheet.Cells[totalRow, col].Style.Numberformat.Format = total == Math.Truncate(total) && total < 10000 ? "#,##0" : "#,##0.00";
        }
        var totalRange = worksheet.Cells[totalRow, 1, totalRow, worksheet.Dimension.End.Column];
        totalRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        totalRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(243, 244, 246));
        totalRange.Style.Border.Top.Style = ExcelBorderStyle.Medium;
    }
}
