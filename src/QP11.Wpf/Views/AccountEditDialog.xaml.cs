using System.Windows;
using QP11.Core.Entities;

namespace QP11.Wpf.Views;

public partial class AccountEditDialog : Window
{
    public string AccountName => txtName.Text.Trim();
    public decimal Amount => decimal.TryParse(txtAmount.Text, out var v) ? v : 0;
    public int FlagValue => cboFlag.SelectedIndex == 1 ? 1 : 0;
    public string AccountType => cboType.Text;
    public string Memo => txtMemo.Text.Trim();

    public AccountEditDialog(Account? entity = null)
    {
        InitializeComponent();
        if (entity != null)
        {
            txtName.Text = entity.Name ?? "";
            txtAmount.Text = entity.Je?.ToString() ?? "";
            cboFlag.SelectedIndex = entity.Flag == 1 ? 1 : 0;
            cboType.Text = entity.Type ?? "现金";
            txtMemo.Text = entity.Memo ?? "";
            Title = "编辑账目";
        }
        else
        {
            Title = "新增账目";
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("请输入名称", "提示");
            return;
        }
        DialogResult = true;
    }
}
