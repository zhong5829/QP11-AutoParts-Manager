using System;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Services;

namespace QP11.Wpf.Views;

public partial class PasswordWindow : Window
{
    private readonly UserInfor _user;
    private readonly IUserRepository _userRepo;

    public PasswordWindow(UserInfor user, IUserRepository userRepo)
    {
        _userRepo = userRepo;
        InitializeComponent();
        _user = user;
    }

    private async void BtnChange_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(txtOldPwd.Password) || string.IsNullOrEmpty(txtNewPwd.Password))
        {
            MessageBox.Show("请填写完整", "提示");
            return;
        }

        if (txtNewPwd.Password != txtConfirmPwd.Password)
        {
            MessageBox.Show("两次密码不一致", "提示");
            return;
        }

        var oldHash = AuthService.Md5Hash(txtOldPwd.Password);
        if (oldHash != _user.Password)
        {
            MessageBox.Show("原密码错误", "提示");
            return;
        }

        try
        {
            var newHash = AuthService.Md5Hash(txtNewPwd.Password);
            await _userRepo.UpdatePasswordAsync(_user.Username!, newHash);
            _user.Password = newHash;
            MessageBox.Show("密码修改成功", "提示");
            Close();
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "修改密码失败"); MessageBox.Show($"修改失败: {ex.Message}", "错误"); }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
}
