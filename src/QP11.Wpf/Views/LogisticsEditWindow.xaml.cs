using System.Windows;
using QP11.Core.Entities;

namespace QP11.Wpf.Views;

public partial class LogisticsEditWindow : Window
{
    public Logistics Entity { get; private set; }
    public bool IsNew { get; }

    public LogisticsEditWindow(Logistics? entity = null)
    {
        InitializeComponent();
        IsNew = (entity == null);
        Title = IsNew ? "新增物流商" : "编辑物流商";
        Entity = entity ?? new Logistics();

        // 填充数据
        txtSid.Text = Entity.Sid ?? "";
        txtName.Text = Entity.Name ?? "";
        txtLinkman.Text = Entity.Linkman ?? "";
        txtTel.Text = Entity.Tel ?? "";
        txtMobile.Text = Entity.Mobile ?? "";
        txtFax.Text = Entity.Fax ?? "";
        txtAddress.Text = Entity.Address ?? "";
        txtZip.Text = Entity.Zip ?? "";
        txtLevel.Text = Entity.Level ?? "";
        txtCredit.Text = Entity.Credit?.ToString() ?? "";
        txtBank.Text = Entity.Bank ?? "";

        if (IsNew) txtName.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        { MessageBox.Show("名称不能为空", "提示"); txtName.Focus(); return; }

        Entity.Sid = txtSid.Text.Trim();
        Entity.Name = txtName.Text.Trim();
        Entity.Linkman = txtLinkman.Text.Trim();
        Entity.Tel = txtTel.Text.Trim();
        Entity.Mobile = txtMobile.Text.Trim();
        Entity.Fax = txtFax.Text.Trim();
        Entity.Address = txtAddress.Text.Trim();
        Entity.Zip = txtZip.Text.Trim();
        Entity.Level = txtLevel.Text.Trim();

        if (decimal.TryParse(txtCredit.Text, out var credit)) Entity.Credit = credit;
        else Entity.Credit = null;

        Entity.Bank = txtBank.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
