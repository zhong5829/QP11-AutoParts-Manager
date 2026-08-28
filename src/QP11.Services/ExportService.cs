using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace QP11.Services;

public class ExportService
{
    public async Task<string> ExportToExcelAsync(DataTable data, string fileName)
    {
        return await Task.Run(() =>
        {
            var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("Sheet1");

            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < data.Columns.Count; i++)
            {
                headerRow.CreateCell(i).SetCellValue(data.Columns[i].ColumnName);
            }

            var headerStyle = workbook.CreateCellStyle();
            var boldFont = workbook.CreateFont();
            boldFont.IsBold = true;
            headerStyle.SetFont(boldFont);
            for (int i = 0; i < data.Columns.Count; i++)
            {
                headerRow.GetCell(i).CellStyle = headerStyle;
            }

            for (int r = 0; r < data.Rows.Count; r++)
            {
                var row = sheet.CreateRow(r + 1);
                for (int c = 0; c < data.Columns.Count; c++)
                {
                    var cell = row.CreateCell(c);
                    var val = data.Rows[r][c];
                    if (val == null || val == DBNull.Value) continue;
                    if (val is decimal dec) cell.SetCellValue((double)dec);
                    else if (val is int intv) cell.SetCellValue(intv);
                    else if (val is long longv) cell.SetCellValue(longv);
                    else if (val is DateTime dt) cell.SetCellValue(dt.ToString("yyyy-MM-dd HH:mm:ss"));
                    else cell.SetCellValue(val.ToString() ?? "");
                }
            }

            for (int i = 0; i < data.Columns.Count; i++)
            {
                AutoFitColumn(sheet, i);
            }

            var dirPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            var path = Path.Combine(dirPath, fileName);
            using var fs = new FileStream(path, FileMode.Create);
            workbook.Write(fs);
            return path;
        });
    }

    public async Task<string> ExportToExcelAsync<T>(IEnumerable<T> items, string fileName, params string[] columns)
    {
        var table = new DataTable();
        var props = typeof(T).GetProperties();

        var selectedProps = columns.Length > 0
            ? props.Where(p => columns.Contains(p.Name)).ToArray()
            : props;

        foreach (var p in selectedProps)
            table.Columns.Add(p.Name);

        foreach (var item in items)
        {
            var row = table.NewRow();
            for (int i = 0; i < selectedProps.Length; i++)
            {
                var val = selectedProps[i].GetValue(item);
                row[i] = val ?? DBNull.Value;
            }
            table.Rows.Add(row);
        }

        return await ExportToExcelAsync(table, fileName);
    }

    public Task<(string? Path, string? Error)> ExportMultiSheetAsync(string fileName, params (DataTable Data, string SheetName, HashSet<int> RedRows)[] sheets)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
        return ExportMultiSheetToPathAsync(path, sheets);
    }

    /// <summary>
    /// 按指定完整路径导出多 sheet Excel（含红色行标记与文件占用检测）
    /// </summary>
    public Task<(string? Path, string? Error)> ExportMultiSheetToPathAsync(string filePath, params (DataTable Data, string SheetName, HashSet<int> RedRows)[] sheets)
    {
        return Task.Run<(string?, string?)>(() =>
        {
            var workbook = new XSSFWorkbook();

            var headerStyle = workbook.CreateCellStyle();
            var boldFont = workbook.CreateFont();
            boldFont.IsBold = true;
            headerStyle.SetFont(boldFont);

            var redFont = workbook.CreateFont();
            redFont.Color = NPOI.HSSF.Util.HSSFColor.Red.Index;
            var redCellStyle = workbook.CreateCellStyle();
            redCellStyle.SetFont(redFont);

            foreach (var (data, sheetName, redRows) in sheets)
            {
                var sheet = workbook.CreateSheet(sheetName);

                var headerRow = sheet.CreateRow(0);
                for (int i = 0; i < data.Columns.Count; i++)
                    headerRow.CreateCell(i).SetCellValue(data.Columns[i].ColumnName);
                for (int i = 0; i < data.Columns.Count; i++)
                    headerRow.GetCell(i).CellStyle = headerStyle;

                for (int r = 0; r < data.Rows.Count; r++)
                {
                    var row = sheet.CreateRow(r + 1);
                    var isRedRow = redRows != null && redRows.Contains(r);
                    for (int c = 0; c < data.Columns.Count; c++)
                    {
                        var cell = row.CreateCell(c);
                        var val = data.Rows[r][c];
                        if (val == null || val == DBNull.Value) continue;
                        if (val is decimal dec) cell.SetCellValue((double)dec);
                        else if (val is int intv) cell.SetCellValue(intv);
                        else if (val is long longv) cell.SetCellValue(longv);
                        else if (val is DateTime dt) cell.SetCellValue(dt.ToString("yyyy-MM-dd"));
                        else cell.SetCellValue(val.ToString() ?? "");

                        if (isRedRow) cell.CellStyle = redCellStyle;
                    }
                }

                for (int i = 0; i < data.Columns.Count; i++)
                    AutoFitColumn(sheet, i);
            }

            var path = filePath;
            try
            {
                using var fs = new FileStream(path, FileMode.Create);
                workbook.Write(fs);
            }
            catch (IOException)
            {
                return (null, $"文件被占用，请关闭已打开的 Excel 文件后重试：{path}");
            }
            return (path, null);
        });
    }

    /// <summary>
    /// 自适应列宽：中文字符按2个宽度计算，NPOI的AutoSizeColumn对中文不准
    /// </summary>
    private static void AutoFitColumn(ISheet sheet, int columnIndex)
    {
        // NPOI列宽单位：1/256个字符宽度
        const int minWidth = 8 * 256;   // 最少8个字符宽
        const int padding = 4 * 256;    // 左右留4字符余量
        int maxWidth = 50 * 256;        // 最大50字符宽，防止单元格内容过长

        int maxLen = 0;
        for (int r = 0; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null) continue;
            var cell = row.GetCell(columnIndex);
            if (cell == null) continue;

            var text = cell.CellType == CellType.String ? cell.StringCellValue ?? "" : cell.ToString() ?? "";
            // 计算显示宽度：ASCII字符=1，非ASCII(中文等)=2
            int len = 0;
            foreach (char c in text)
                len += c > 127 ? 2 : 1;
            if (len > maxLen) maxLen = len;
        }

        int width = Math.Max(minWidth, maxLen * 256 + padding);
        if (width > maxWidth) width = maxWidth;
        sheet.SetColumnWidth(columnIndex, width);
    }
}
