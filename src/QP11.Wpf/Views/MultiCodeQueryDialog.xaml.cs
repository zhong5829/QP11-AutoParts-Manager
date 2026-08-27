using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace QP11.Wpf.Views;

public partial class MultiCodeQueryDialog : Window
{
    /// <summary>解析后的配件编号列表（已去空、去重、Trim）</summary>
    public List<string> Codes { get; private set; } = new();

    public MultiCodeQueryDialog()
    {
        InitializeComponent();
        txtCodes.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Codes = txtCodes.Text
            .Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();

        if (Codes.Count == 0)
        {
            MessageBox.Show("请输入至少一个配件编号", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            txtCodes.Focus();
            return;
        }

        DialogResult = true;
    }
}
