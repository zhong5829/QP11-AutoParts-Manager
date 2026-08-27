using System.Windows;
using QP11.Core.Entities;
using QP11.Services;

namespace QP11.Wpf.Views;

public partial class MemoConfirmDialog : Window
{
    private readonly UserInfor _user;

    public MemoConfirmDialog(UserInfor user)
    {
        InitializeComponent();
        _user = user;
        txtPwd.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(txtPwd.Password))
        {
            MessageBox.Show("请输入密码", "提示");
            return;
        }

        var hash = AuthService.Md5Hash(txtPwd.Password);
        if (hash != _user.Password)
        {
            MessageBox.Show("密码错误", "提示");
            txtPwd.Clear();
            txtPwd.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
