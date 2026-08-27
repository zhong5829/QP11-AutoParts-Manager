using System;
using System.Collections.ObjectModel;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Services;

namespace QP11.Wpf.Views;

public partial class UserManagerWindow : Window
{
    private readonly IUserRepository _userRepo;
    public ObservableCollection<UserInfor> Users { get; } = new();

    public UserManagerWindow(IUserRepository userRepo)
    {
        _userRepo = userRepo;
        InitializeComponent();
        dgUsers.ItemsSource = Users;
        LoadUsers();
    }

    private async void LoadUsers()
    {
        try
        {
            Users.Clear();
            var data = await _userRepo.GetAllIncludingDisabledAsync();
            foreach (var u in data) Users.Add(u);
            txtCount.Text = $"共 {Users.Count} 条记录";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载用户失败: {ex.Message}", "错误");
        }
    }

    private async void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var uid = InputBoxDialog.Show("请输入用户名:", "新增用户");
        if (string.IsNullOrWhiteSpace(uid)) return;
        var name = InputBoxDialog.Show("请输入姓名:", "新增用户");
        var pwd = InputBoxDialog.Show("请输入初始密码:", "新增用户", "123456");

        try
        {
            await _userRepo.InsertAsync(new UserInfor { Username = uid.Trim(), Password = AuthService.Md5Hash(pwd!), Name = name!.Trim(), State = 1 });
            LoadUsers();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"新增失败: {ex.Message}", "错误");
        }
    }

    private async void BtnChangePwd_Click(object sender, RoutedEventArgs e)
    {
        if (dgUsers.SelectedItem is not UserInfor user) return;
        var newPwd = InputBoxDialog.Show($"修改用户 [{user.Name}] 的密码:", "修改密码");
        if (string.IsNullOrWhiteSpace(newPwd)) return;
        try
        {
            await _userRepo.UpdatePasswordAsync(user.Username!, AuthService.Md5Hash(newPwd));
            MessageBox.Show("密码修改成功", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"修改失败: {ex.Message}", "错误");
        }
    }

    private async void BtnDisable_Click(object sender, RoutedEventArgs e)
    {
        if (dgUsers.SelectedItem is not UserInfor user) return;
        if (MessageBox.Show($"确定禁用用户 [{user.Name}]?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try
        {
            await _userRepo.DisableAsync(user.Username!);
            LoadUsers();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"禁用失败: {ex.Message}", "错误");
        }
    }

    private async void BtnEnable_Click(object sender, RoutedEventArgs e)
    {
        if (dgUsers.SelectedItem is not UserInfor user) return;
        try
        {
            await _userRepo.EnableAsync(user.Username!);
            LoadUsers();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启用失败: {ex.Message}", "错误");
        }
    }
}
