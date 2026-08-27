using System;
using System.Collections.Generic;
using System.Windows;
using QP11.Wpf.Services;

namespace QP11.Wpf.Views;

public partial class SettlementResultDialog : Window
{
    /// <summary>用户是否勾选了"立即打印"</summary>
    public bool PrintNow => chkPrintNow.IsChecked == true;

    /// <summary>待打印的单据数据（由调用方设置，勾选打印时使用）</summary>
    public BillPrintData? PrintData { get; set; }

    public SettlementResultDialog(string billNo, decimal billTotal, decimal totalPaid, decimal arrear)
    {
        InitializeComponent();

        txtBillNo.Text = billNo;
        txtTotal.Text = $"¥{billTotal:N2}";
        txtPaid.Text = $"¥{totalPaid:N2}";
        txtArrear.Text = $"¥{arrear:N2}";
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (chkPrintNow.IsChecked == true && PrintData != null)
        {
            try
            {
                SilentPrintHelper.Print(PrintData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打印失败: {ex.Message}\n可稍后在查询模式中手动打印", "打印提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        DialogResult = true;
    }
}

/// <summary>
/// 静默打印助手：构建 FlowDocument 并直接发送到配置的打印机
/// </summary>
public static class SilentPrintHelper
{
    public static void Print(BillPrintData billData)
    {
        var settings = PrintSettingsService.Load();
        var billType = billData.BillType ?? "销售";
        var columns = settings.BillPrint.GetColumns(billType);

        var doc = BillDocumentBuilder.Build(billData, columns, settings);
        var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;

        var printerName = settings.PagePrint.PrinterName;
        if (string.IsNullOrEmpty(printerName))
        {
            var printServer = new System.Printing.LocalPrintServer();
            var defaultQueue = printServer.DefaultPrintQueue;
            printerName = defaultQueue?.Name;
        }

        if (string.IsNullOrEmpty(printerName))
            throw new InvalidOperationException("未找到可用打印机");

        var printServer2 = new System.Printing.LocalPrintServer();
        var queue = printServer2.GetPrintQueue(printerName)
                    ?? throw new InvalidOperationException($"未找到打印机: {printerName}");

        var copies = settings.PagePrint.Copies > 0 ? settings.PagePrint.Copies : 1;
        var ticket = queue.DefaultPrintTicket.Clone();
        ticket.CopyCount = copies;

        var writer = System.Printing.PrintQueue.CreateXpsDocumentWriter(queue);
        writer.Write(paginator, ticket);
    }
}
