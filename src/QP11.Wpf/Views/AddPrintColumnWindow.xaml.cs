using System.Windows;
using QP11.Wpf.Services;

namespace QP11.Wpf.Views;

public partial class AddPrintColumnWindow : Window
{
    public PrintColumnConfig ResultConfig { get; private set; } = new();

    public AddPrintColumnWindow()
    {
        InitializeComponent();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtHeader.Text))
        {
            MessageBox.Show("请输入列标题", "提示");
            return;
        }

        var dataField = cboDataField.Text.Trim();
        if (string.IsNullOrWhiteSpace(dataField))
        {
            MessageBox.Show("请选择或输入绑定字段", "提示");
            return;
        }

        if (!double.TryParse(txtWidth.Text, out var width) || width <= 0)
        {
            MessageBox.Show("请输入有效的列宽", "提示");
            return;
        }

        ResultConfig = new PrintColumnConfig
        {
            Key = string.IsNullOrWhiteSpace(txtKey.Text) ? dataField.ToLower() : txtKey.Text.Trim(),
            Header = txtHeader.Text.Trim(),
            Width = width,
            Visible = true,
            DataField = dataField,
            Format = string.IsNullOrEmpty(cboFormat.Text) ? null : cboFormat.Text.Trim(),
            Alignment = cboAlignment.Text.Trim(),
        };

        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
