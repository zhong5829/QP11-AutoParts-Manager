using System;
using System.Drawing.Printing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QP11.Wpf.Services;

namespace QP11.Wpf.Views;

public partial class PrintSetupWindow : Window
{
    public PrintSetupWindow()
    {
        InitializeComponent();
        LoadPrinters();
        LoadSettings();
    }

    private void LoadPrinters()
    {
        cmbPrinter.Items.Clear();
        foreach (string name in PrinterSettings.InstalledPrinters)
        {
            cmbPrinter.Items.Add(name);
        }
    }

    private void LoadSettings()
    {
        var settings = PrintSettingsService.Load();
        var page = settings.PagePrint;

        if (!string.IsNullOrEmpty(page.PrinterName))
        {
            for (int i = 0; i < cmbPrinter.Items.Count; i++)
            {
                if (cmbPrinter.Items[i].ToString() == page.PrinterName)
                {
                    cmbPrinter.SelectedIndex = i;
                    break;
                }
            }
        }
        if (cmbPrinter.SelectedIndex < 0 && cmbPrinter.Items.Count > 0)
            cmbPrinter.SelectedIndex = 0;

        SelectComboBoxItemByTag(cmbPaperSize, page.PaperSize);
        SelectComboBoxItemByTag(cmbOrientation, page.Orientation);

        txtCopies.Text = page.Copies.ToString();
        chkColor.IsChecked = page.Color;

        txtMarginTop.Text = page.MarginTop.ToString();
        txtMarginBottom.Text = page.MarginBottom.ToString();
        txtMarginLeft.Text = page.MarginLeft.ToString();
        txtMarginRight.Text = page.MarginRight.ToString();
    }

    private void SelectComboBoxItemByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private string GetSelectedTag(ComboBox combo)
    {
        return (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
    }

    private void BtnPrinterProps_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var ps = new PrinterSettings { PrinterName = cmbPrinter.Text };
            var dlg = new PrintDialog();
            dlg.PrintQueue = new System.Printing.PrintQueue(new System.Printing.PrintServer(), cmbPrinter.Text);
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "打开打印机设置失败");
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var settings = PrintSettingsService.Load();
        var page = settings.PagePrint;

        page.PrinterName = cmbPrinter.Text;
        page.PaperSize = GetSelectedTag(cmbPaperSize);
        page.Orientation = GetSelectedTag(cmbOrientation);
        page.Copies = int.TryParse(txtCopies.Text, out var c) && c > 0 ? c : 1;
        page.Color = chkColor.IsChecked == true;

        page.MarginTop = double.TryParse(txtMarginTop.Text, out var mt) ? mt : 20;
        page.MarginBottom = double.TryParse(txtMarginBottom.Text, out var mb) ? mb : 20;
        page.MarginLeft = double.TryParse(txtMarginLeft.Text, out var ml) ? ml : 20;
        page.MarginRight = double.TryParse(txtMarginRight.Text, out var mr) ? mr : 20;

        PrintSettingsService.Save(settings);
        MessageBox.Show("打印设置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        var parent = Window.GetWindow(this);
        if (parent != null) parent.Close();
    }
}
