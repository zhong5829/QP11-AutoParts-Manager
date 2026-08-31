using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Interfaces;
using QP11.Services;
using QP11.Wpf.Services;
using QP11.Wpf.Services.LabelPrint;

namespace QP11.Wpf.Views;

public class BillPrintData
{
    public string? BillType { get; set; }
    public string? Sn { get; set; }
    public string? DateText { get; set; }
    public string? PartnerName { get; set; }
    public string? PartnerPhone { get; set; }
    public string? PartnerContact { get; set; }
    public string? PartnerAddress { get; set; }
    public string? WorkerName { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyAddress { get; set; }
    public string? CompanyPhone { get; set; }
    public string? Memo { get; set; }
    public List<BillPrintItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal Cash { get; set; }
    public decimal Weixin { get; set; }
    public decimal Zhifubao { get; set; }
    public decimal Arrearage { get; set; }
    public string? PaymentMethod { get; set; }
    public string? DeliveryMethod { get; set; }

    /// <summary>从数据库加载公司信息并填充到当前实例</summary>
    public async Task LoadCompanyInfoAsync()
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var row = await db.QueryFirstOrDefaultAsync<dynamic>("SELECT TOP 1 qc, tel, mobile, address FROM business_infor");
            if (row != null)
            {
                CompanyName = row.qc?.ToString() ?? "";
                CompanyPhone = row.tel?.ToString() ?? row.mobile?.ToString() ?? "";
                CompanyAddress = row.address?.ToString() ?? "";
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载公司信息失败");
        }
    }
}

public class BillPrintItem
{
    public int Index { get; set; }
    public string? PartNo { get; set; }
    public string? PartName { get; set; }
    public string? Cartype { get; set; }
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal PfPrice { get; set; }
    public int Amount { get; set; }
    public decimal Subtotal { get; set; }
    public string? Place { get; set; }
    public string? Area { get; set; }
    public string? Memo { get; set; }
    public string? Brand { get; set; }
    public decimal BillPrice { get; set; }
    public decimal DiscountRate { get; set; }
}

public partial class PrintPreviewWindow : Window
{
    private readonly BillPrintData? _billData;
    private readonly DataTable? _tableData;
    private readonly string _title;

    public PrintPreviewWindow(BillPrintData billData, string title = "打印预览")
    {
        InitializeComponent();
        _billData = billData;
        _title = title;
        Title = title;
        LoadPrinters();
        LoadPrintSettings();
        BuildBillDocument();
    }

    public PrintPreviewWindow(DataTable data, string title = "打印预览")
    {
        InitializeComponent();
        _tableData = data;
        _title = title;
        Title = title;
        rbStyle1.Visibility = Visibility.Collapsed;
        rbStyle2.Visibility = Visibility.Collapsed;
        rbStyle3.Visibility = Visibility.Collapsed;
        txtHeight.Visibility = Visibility.Collapsed;
        LoadPrinters();
        BuildTableDocument();
    }

    private void LoadPrinters()
    {
        cmbPrinter.Items.Clear();
        var printServer = new System.Printing.LocalPrintServer();
        foreach (var queue in printServer.GetPrintQueues())
            cmbPrinter.Items.Add(queue.Name);

        // 选中配置的默认打印机
        var settings = PrintSettingsService.Load();
        var savedName = settings.PagePrint.PrinterName;
        if (!string.IsNullOrEmpty(savedName))
        {
            for (int i = 0; i < cmbPrinter.Items.Count; i++)
            {
                if (cmbPrinter.Items[i]?.ToString() == savedName)
                {
                    cmbPrinter.SelectedIndex = i;
                    break;
                }
            }
        }
        if (cmbPrinter.SelectedIndex < 0 && cmbPrinter.Items.Count > 0)
            cmbPrinter.SelectedIndex = 0;

        txtCopies.Text = settings.PagePrint.Copies.ToString();
    }

    private void LoadPrintSettings()
    {
        var settings = PrintSettingsService.Load();
        var bill = settings.BillPrint;

        rbStyle1.IsChecked = bill.PrintStyle == 1;
        rbStyle2.IsChecked = bill.PrintStyle == 2;
        rbStyle3.IsChecked = bill.PrintStyle == 3;

        var height = bill.PrintStyle switch
        {
            2 => bill.PrintHeight2,
            3 => bill.PrintHeight3,
            _ => bill.PrintHeight1
        };
        txtHeight.Text = height.ToString();
    }

    private int GetStyle()
    {
        return rbStyle1.IsChecked == true ? 1 : rbStyle2.IsChecked == true ? 2 : 3;
    }

    private void Style_Changed(object sender, RoutedEventArgs e)
    {
        var settings = PrintSettingsService.Load();
        var height = GetStyle() switch
        {
            2 => settings.BillPrint.PrintHeight2,
            3 => settings.BillPrint.PrintHeight3,
            _ => settings.BillPrint.PrintHeight1
        };
        txtHeight.Text = height.ToString();
        if (_billData != null) BuildBillDocument();
    }

    private void BtnApplyHeight_Click(object sender, RoutedEventArgs e)
    {
        if (_billData != null) BuildBillDocument();
    }

    private void BuildBillDocument()
    {
        if (_billData == null) return;

        // 应用高度到设置
        var settings = PrintSettingsService.Load();
        if (double.TryParse(txtHeight.Text, out var h) && h >= 0 && h <= 999)
        {
            switch (settings.BillPrint.PrintStyle)
            {
                case 1: settings.BillPrint.PrintHeight1 = h; break;
                case 2: settings.BillPrint.PrintHeight2 = h; break;
                case 3: settings.BillPrint.PrintHeight3 = h; break;
            }
        }
        settings.BillPrint.PrintStyle = GetStyle();

        // 获取对应单据类型的列配置
        var billType = _billData.BillType ?? "销售";
        var columns = settings.BillPrint.GetColumns(billType);

        var doc = BillDocumentBuilder.Build(_billData, columns, settings, true, OpenLabelPrintDialog);
        docReader.Document = doc;
    }

    private void BuildTableDocument()
    {
        if (_tableData == null || _tableData.Rows.Count == 0) return;

        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Microsoft YaHei"),
            FontSize = 12,
            PagePadding = new Thickness(40)
        };

        var titlePara = new Paragraph(new Run(_title))
        {
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center
        };
        doc.Blocks.Add(titlePara);

        doc.Blocks.Add(new Paragraph(new Run($"打印时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"))
        {
            FontSize = 10,
            Foreground = Brushes.Gray,
            TextAlignment = TextAlignment.Right
        });

        var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0.5) };
        for (int i = 0; i < _tableData.Columns.Count; i++)
            table.Columns.Add(new TableColumn());

        var rowGroup = new TableRowGroup();

        var headerRow = new TableRow { Background = Brushes.LightGray };
        foreach (DataColumn col in _tableData.Columns)
        {
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(col.ColumnName)))
            {
                FontWeight = FontWeights.Bold,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(4)
            });
        }
        rowGroup.Rows.Add(headerRow);

        foreach (DataRow dr in _tableData.Rows)
        {
            var row = new TableRow();
            foreach (DataColumn col in _tableData.Columns)
            {
                row.Cells.Add(new TableCell(new Paragraph(new Run(dr[col]?.ToString() ?? "")))
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(4)
                });
            }
            rowGroup.Rows.Add(row);
        }

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);

        doc.Blocks.Add(new Paragraph(new Run($"共 {_tableData.Rows.Count} 条记录"))
        {
            FontSize = 10,
            Foreground = Brushes.Gray
        });

        docReader.Document = doc;
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        var printerName = cmbPrinter.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(printerName))
        {
            MessageBox.Show("未选择打印机", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var copies = 1;
        if (int.TryParse(txtCopies.Text, out var c) && c > 0)
            copies = c;

        try
        {
            // 保存当前打印机选择和份数到设置
            var settings = PrintSettingsService.Load();
            settings.PagePrint.PrinterName = printerName;
            settings.PagePrint.Copies = copies;
            PrintSettingsService.Save(settings);

            // 单据打印：用不含“标签打印”操作列的干净文档发送打印，与“保存时立即打印”布局完全一致，
            // 避免预览交互列抬高行高导致打印分页时表头与第一行之间出现空白
            // 对账单等 DataTable 打印无操作列，直接使用预览文档
            var doc = _billData != null
                ? BillDocumentBuilder.Build(_billData,
                    settings.BillPrint.GetColumns(_billData.BillType ?? "销售"), settings)
                : docReader.Document;

            var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            var printServer = new System.Printing.LocalPrintServer();
            var queue = printServer.GetPrintQueue(printerName)
                        ?? throw new InvalidOperationException($"未找到打印机: {printerName}");

            // 使用独立的PrintTicket设置份数，避免Commit需要管理员权限
            var ticket = queue.DefaultPrintTicket.Clone();
            ticket.CopyCount = copies;

            var writer = System.Printing.PrintQueue.CreateXpsDocumentWriter(queue);
            writer.Write(paginator, ticket);

            // 打印后重新赋值文档，防止预览变空白
            docReader.Document = null;
            docReader.Document = doc;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打印失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var data = _billData != null ? BillDataToDataTable() : _tableData;
        if (data == null) return;
        try
        {
            var exportService = new ExportService();
            var path = await exportService.ExportToExcelAsync(data, $"{_title}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            MessageBox.Show($"导出成功!\n文件: {path}", "提示");
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "导出Excel失败"); MessageBox.Show($"导出失败: {ex.Message}", "错误"); }
    }

    private DataTable BillDataToDataTable()
    {
        if (_billData == null) return new DataTable();

        // 根据当前列配置生成导出表
        var settings = PrintSettingsService.Load();
        var billType = _billData.BillType ?? "销售";
        var columns = settings.BillPrint.GetColumns(billType)
            .Where(c => c.Visible)
            .OrderBy(c => c.Order)
            .ToList();

        var dt = new DataTable();
        foreach (var col in columns)
            dt.Columns.Add(col.Header, typeof(string));

        foreach (var item in _billData.Items)
        {
            var values = new List<string>();
            foreach (var col in columns)
            {
                var field = col.DataField ?? col.Key;
                values.Add(field switch
                {
                    "Index" or "index" => item.Index.ToString(),
                    "PartNo" or "partno" => item.PartNo ?? "",
                    "PartName" or "name" => item.PartName ?? "",
                    "Cartype" or "cartype" => item.Cartype ?? "",
                    "Price" or "price" => item.Price > 0 ? item.Price.ToString(col.Format ?? "N2") : "",
                    "PfPrice" or "pfprice" => item.PfPrice > 0 ? item.PfPrice.ToString(col.Format ?? "N2") : "",
                    "BillPrice" or "billprice" => item.BillPrice > 0 ? item.BillPrice.ToString(col.Format ?? "N2") : "",
                    "Unit" or "unit" => item.Unit ?? "",
                    "Amount" or "amount" => Math.Abs(item.Amount).ToString(),
                    "Subtotal" or "subtotal" => Math.Abs(item.Subtotal).ToString(col.Format ?? "N2"),
                    "Place" or "place" => item.Place ?? "",
                    "Area" or "area" => item.Area ?? "",
                    "Brand" or "brand" => item.Brand ?? "",
                    "DiscountRate" or "discount" => item.DiscountRate > 0 ? item.DiscountRate.ToString("N0") + "%" : "",
                    "Memo" or "memo" => item.Memo ?? "",
                    _ => ""
                });
            }
            dt.Rows.Add(values.ToArray());
        }
        return dt;
    }

    /// <summary>单据预览“标签打印”按钮回调：读取该行编码/名称/车型，弹出标签打印对话框</summary>
    private void OpenLabelPrintDialog(BillPrintItem item)
    {
        var dlg = new LabelPrintDialog(new LabelPrintItem
        {
            PartNo = item.PartNo,
            Name = item.PartName,
            CarType = item.Cartype
        })
        { Owner = this };
        dlg.ShowDialog();
    }
}
