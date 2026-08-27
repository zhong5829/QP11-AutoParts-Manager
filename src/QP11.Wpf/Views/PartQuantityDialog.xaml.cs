using System;
using System.Windows;
using System.Windows.Controls;

namespace QP11.Wpf.Views;

public partial class PartQuantityDialog : Window
{
    /// <summary>确认后的数量</summary>
    public decimal ResultAmount { get; private set; }

    /// <summary>确认后的采购价</summary>
    public decimal ResultInPrice { get; private set; }

    public PartQuantityDialog(string partNo, string partName, string cartype, string unit,
        decimal defaultInPrice, decimal defaultAmount = 1)
    {
        InitializeComponent();

        txtPartInfo.Text = $"{partNo}  {partName}";
        txtPartDetail.Text = $"车型: {cartype}  单位: {unit}";
        txtInPrice.Text = defaultInPrice.ToString();
        txtAmount.Text = defaultAmount.ToString();

        txtAmount.TextChanged += (_, _) => UpdateSubTotal();
        txtInPrice.TextChanged += (_, _) => UpdateSubTotal();
        UpdateSubTotal();

        txtAmount.Focus();
        txtAmount.SelectAll();

        txtAmount.KeyDown += TxtInput_KeyDown;
        txtInPrice.KeyDown += TxtInput_KeyDown;
    }

    private void UpdateSubTotal()
    {
        var amount = decimal.TryParse(txtAmount.Text, out var a) ? a : 0;
        var price = decimal.TryParse(txtInPrice.Text, out var p) ? p : 0;
        txtSubTotal.Text = Math.Round(amount * price, 2).ToString("N2");
    }

    private void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
            tb.SelectAll();
    }

    /// <summary>
    /// 数量框回车 → 跳到采购价并全选；采购价框回车 → 确定
    /// </summary>
    private void TxtInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        e.Handled = true;

        if (sender == txtAmount)
        {
            txtInPrice.Focus();
            txtInPrice.SelectAll();
        }
        else if (sender == txtInPrice)
        {
            BtnConfirm_Click(sender, e);
        }
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        var amount = decimal.TryParse(txtAmount.Text, out var a) ? a : 0;
        if (amount <= 0)
        {
            MessageBox.Show("数量必须大于0", "提示");
            txtAmount.Focus();
            return;
        }

        var price = decimal.TryParse(txtInPrice.Text, out var p) ? p : 0;

        ResultAmount = amount;
        ResultInPrice = price;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
