using System;
using System.Windows;

namespace QP11.Wpf.Views;

public partial class ConfirmPaymentDialog : Window
{
    public string PayMethod { get; private set; } = "现金";
    public decimal Amount { get; private set; }

    public ConfirmPaymentDialog(int recordCount, decimal totalAmount)
    {
        InitializeComponent();
        txtInfo.Text = $"确认 {recordCount} 条记录共 {totalAmount:N2} 元已到账?";
        txtAmount.Text = totalAmount.ToString("N2");
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount == 0)
        {
            MessageBox.Show("请输入有效的收款金额", "提示");
            return;
        }

        Amount = amount;

        if (rbCash.IsChecked == true) PayMethod = "现金";
        else if (rbWeixin.IsChecked == true) PayMethod = "微信";
        else if (rbZhifubao.IsChecked == true) PayMethod = "支付宝";

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
