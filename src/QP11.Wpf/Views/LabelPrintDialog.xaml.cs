using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using QP11.Wpf.Services;
using QP11.Wpf.Services.LabelPrint;

namespace QP11.Wpf.Views;

/// <summary>
/// 标签打印对话框：由单据打印预览的「标签打印」按钮唤起，
/// 读取该行编码/名称/车型，支持数量、模板、打印机设置，确定后静默打印。
/// </summary>
public partial class LabelPrintDialog : Window
{
    private readonly LabelPrintItem _baseItem;

    public LabelPrintDialog(LabelPrintItem item)
    {
        InitializeComponent();
        // 固定引用：预览编辑器与打印都操作同一份数据（双击修改后立即生效）
        _baseItem = new LabelPrintItem { PartNo = item.PartNo, Name = item.Name, CarType = item.CarType };
        txtPartNo.Text = _baseItem.PartNo;
        txtName.Text = _baseItem.Name;
        txtCarType.Text = _baseItem.CarType;
        LoadTemplates();
        LoadPrinters();
        editor.ItemsChanged += (_, _) => SyncInputsFromEditor();
        BuildPreview();
    }

    private void LoadTemplates()
    {
        cboTemplate.Items.Clear();
        foreach (var tpl in LabelTemplateService.GetAll())
            cboTemplate.Items.Add(tpl);
        if (cboTemplate.Items.Count > 0)
            cboTemplate.SelectedIndex = 0;
    }

    private void LoadPrinters()
    {
        cboPrinter.Items.Clear();
        var printServer = new System.Printing.LocalPrintServer();
        foreach (var queue in printServer.GetPrintQueues())
            cboPrinter.Items.Add(queue.Name);

        var settings = PrintSettingsService.Load();
        var savedName = settings.PagePrint.PrinterName;
        if (!string.IsNullOrEmpty(savedName))
        {
            for (int i = 0; i < cboPrinter.Items.Count; i++)
            {
                if (cboPrinter.Items[i]?.ToString() == savedName)
                {
                    cboPrinter.SelectedIndex = i;
                    break;
                }
            }
        }
        if (cboPrinter.SelectedIndex < 0 && cboPrinter.Items.Count > 0)
            cboPrinter.SelectedIndex = 0;
    }

    private LabelPrintItem CurrentItem()
    {
        // 同步输入框到基准行（双击修改由 ItemsChanged 回填输入框，此处保持一致）
        _baseItem.PartNo = txtPartNo.Text;
        _baseItem.Name = txtName.Text;
        _baseItem.CarType = txtCarType.Text;
        return _baseItem;
    }

    /// <summary>预览中双击修改文字后回填输入框</summary>
    private void SyncInputsFromEditor()
    {
        if (editor.Items.Count == 0) return;
        var it = editor.Items[0];
        txtPartNo.Text = it.PartNo ?? "";
        txtName.Text = it.Name ?? "";
        if (txtCarType.Text != (it.CarType ?? ""))
            txtCarType.Text = it.CarType ?? "";
    }

    private int CurrentCopies()
    {
        if (int.TryParse(txtCopies.Text, out var c) && c > 0 && c <= 9999) return c;
        return 1;
    }

    private void Template_Changed(object sender, RoutedEventArgs e) => BuildPreview();

    private void Copies_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) => BuildPreview();

    /// <summary>车型编辑实时刷新预览（仅本次打印生效）</summary>
    private void CarType_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) => BuildPreview();

    private void BuildPreview()
    {
        if (cboTemplate.SelectedItem is not LabelTemplate tpl) return;
        editor.SetTemplate(tpl);
        editor.SetItems(new[] { CurrentItem() });
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        var printerName = cboPrinter.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(printerName))
        {
            MessageBox.Show("请选择打印机", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (cboTemplate.SelectedItem is not LabelTemplate tpl)
        {
            MessageBox.Show("请选择标签模板", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var items = Enumerable.Repeat(editor.Items.FirstOrDefault() ?? CurrentItem(), CurrentCopies());
            var (doc, _) = LabelLayoutBuilder.Build(items, tpl);
            LabelPrintHelper.Print(doc, printerName, tpl);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打印失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

/// <summary>标签静默打印辅助：FixedDocument + PrintQueue + 模板纸张尺寸</summary>
public static class LabelPrintHelper
{
    /// <summary>
    /// 静默打印标签文档。PageMediaSize 以模板页尺寸（mm）换算为 1/96 英寸交给驱动。
    /// </summary>
    public static void Print(FixedDocument doc, string printerName, LabelTemplate tpl)
    {
        var printServer = new System.Printing.LocalPrintServer();
        var queue = printServer.GetPrintQueue(printerName)
                    ?? throw new InvalidOperationException($"未找到打印机: {printerName}");

        var pp = doc.DocumentPaginator.PageSize;
        var ticket = queue.DefaultPrintTicket.Clone();
        ticket.CopyCount = 1;
        ticket.PageMediaSize = new System.Printing.PageMediaSize((int)pp.Width, (int)pp.Height);

        var writer = System.Printing.PrintQueue.CreateXpsDocumentWriter(queue);
        writer.Write(doc.DocumentPaginator, ticket);
    }
}