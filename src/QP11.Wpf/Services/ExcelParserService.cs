using System;
using System.Collections.Generic;
using System.IO;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;

namespace QP11.Wpf.Services;

public class ExcelParseResult
{
    public List<string> Headers { get; set; } = new();
    public List<List<string?>> Rows { get; set; } = new();
    public int TotalRows { get; set; }
    public List<string> SheetNames { get; set; } = new();
}

public class ExcelParserService
{
    public ExcelParseResult Parse(string filePath, int sheetIndex = 0, int headerRowIndex = 0, int startDataRow = 1)
    {
        var result = new ExcelParseResult();

        using var stream = OpenFileForRead(filePath);
        IWorkbook workbook;

        var ext = Path.GetExtension(filePath).ToLower();
        if (ext == ".xls")
            workbook = new HSSFWorkbook(stream);
        else if (ext == ".xlsx")
            workbook = new XSSFWorkbook(stream);
        else
            throw new NotSupportedException($"不支持的文件格式: {ext}");

        for (int i = 0; i < workbook.NumberOfSheets; i++)
            result.SheetNames.Add(workbook.GetSheetName(i));

        if (sheetIndex >= workbook.NumberOfSheets)
            sheetIndex = 0;

        var sheet = workbook.GetSheetAt(sheetIndex);
        if (sheet == null || sheet.LastRowNum < 0)
            return result;

        var headerRow = sheet.GetRow(headerRowIndex);
        if (headerRow != null)
        {
            for (int c = 0; c <= headerRow.LastCellNum; c++)
            {
                var cell = headerRow.GetCell(c);
                result.Headers.Add(GetCellText(cell));
            }
        }

        for (int r = Math.Max(startDataRow, headerRowIndex + 1); r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null) continue;

            var isEmpty = true;
            var rowData = new List<string?>();
            for (int c = 0; c < result.Headers.Count; c++)
            {
                var cell = row.GetCell(c);
                var text = GetCellText(cell);
                if (!string.IsNullOrWhiteSpace(text)) isEmpty = false;
                rowData.Add(text);
            }
            if (!isEmpty)
            {
                result.Rows.Add(rowData);
                result.TotalRows++;
            }
        }

        return result;
    }

    private static string GetCellText(ICell? cell)
    {
        if (cell == null) return "";

        switch (cell.CellType)
        {
            case CellType.String:
                return cell.StringCellValue?.Trim() ?? "";
            case CellType.Numeric:
                if (DateUtil.IsCellDateFormatted(cell))
                    return cell.DateCellValue?.ToString("yyyy-MM-dd") ?? "";
                var d = cell.NumericCellValue;
                if (d == Math.Floor(d))
                    return ((long)d).ToString();
                return d.ToString();
            case CellType.Boolean:
                return cell.BooleanCellValue ? "1" : "0";
            case CellType.Formula:
                try
                {
                    return cell.StringCellValue?.Trim() ?? "";
                }
                catch
                {
                    try
                    {
                        var fVal = cell.NumericCellValue;
                        if (fVal == Math.Floor(fVal))
                            return ((long)fVal).ToString();
                        return fVal.ToString();
                    }
                    catch
                    {
                        return "";
                    }
                }
            default:
                return "";
        }
    }

    /// <summary>打开文件，文件被占用时抛出友好提示异常</summary>
    private static FileStream OpenFileForRead(string filePath)
    {
        try
        {
            return File.OpenRead(filePath);
        }
        catch (IOException ex) when (ex.HResult == -2147024864)
        {
            throw new InvalidOperationException(
                $"文件被占用，无法读取。\n\n请关闭正在使用该文件的程序（如 Excel）后重新导入。\n\n文件: {filePath}", ex);
        }
    }
}
