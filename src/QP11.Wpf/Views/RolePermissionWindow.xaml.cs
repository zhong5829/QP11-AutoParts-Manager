using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public class RoleItem
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

public partial class RolePermissionWindow : Window
{
    private readonly List<CheckBox> _permCheckBoxes = new();
    private static readonly string[] PermissionKeys = new[]
    {
        "sell_order", "sell_query", "sell_return", "sell_edit", "sell_exchange",
        "buy_order", "buy_query", "buy_return",
        "part_manage", "inventory", "stock_check", "stock_alert", "price_manage",
        "client_manage", "supplier_manage",
        "account_manage", "payment_entry", "arrearage",
        "member_manage", "borrow_manage",
        "report_center", "ranking",
        "user_manage", "role_manage", "sys_log", "settings",
        "pos", "quotation", "car_mark"
    };

    private static readonly string[] PermissionNames = new[]
    {
        "销售开单", "销售查询", "销售退货", "销售编辑", "销售换货",
        "采购开单", "采购退货",
        "配件管理", "库存查询", "库存盘点", "库存预警", "价格管理",
        "客户管理", "供应商管理",
        "账户管理", "收款录入", "应收应付",
        "会员管理", "借还管理",
        "报表中心", "排行榜",
        "用户管理", "角色权限", "操作日志", "系统设置",
        "零售开单", "报价管理", "车辆档案"
    };

    public RolePermissionWindow()
    {
        InitializeComponent();
        LoadRoles();
    }

    private async void LoadRoles()
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var roles = (await db.QueryAsync<RoleItem>(
                "SELECT code AS Id, name AS Name FROM mnu ORDER BY code")).ToList();
            lbRoles.ItemsSource = roles;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载角色失败: {ex.Message}", "错误");
        }
    }

    private async void LbRoles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lbRoles.SelectedItem is not RoleItem role) return;
        txtRoleName.Text = role.Name;

        spPermissions.Children.Clear();
        _permCheckBoxes.Clear();

        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var row = await db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT auth FROM mnu WHERE code = @Code", new { Code = role.Id });
            var authStr = row != null ? (string?)row.auth : "";
            var perms = (authStr ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).ToHashSet();

            for (int i = 0; i < PermissionKeys.Length; i++)
            {
                var cb = new CheckBox
                {
                    Content = PermissionNames[i],
                    Tag = PermissionKeys[i],
                    IsChecked = perms.Contains(PermissionKeys[i]),
                    Margin = new Thickness(5, 3, 5, 3)
                };
                _permCheckBoxes.Add(cb);
                spPermissions.Children.Add(cb);
            }
        }
        catch
        {
            for (int i = 0; i < PermissionKeys.Length; i++)
            {
                var cb = new CheckBox
                {
                    Content = PermissionNames[i],
                    Tag = PermissionKeys[i],
                    IsChecked = true,
                    Margin = new Thickness(5, 3, 5, 3)
                };
                _permCheckBoxes.Add(cb);
                spPermissions.Children.Add(cb);
            }
        }
    }

    private async void BtnAddRole_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputBoxDialog("请输入角色代码", "角色代码", "") { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.InputText))
        {
            try
            {
                var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
                var allPerms = string.Join(",", PermissionKeys);
                await db.ExecuteAsync("INSERT INTO mnu (code, name, auth) VALUES (@Code, @Name, @Auth)",
                    new { Code = dlg.InputText.Trim(), Name = dlg.InputText.Trim(), Auth = allPerms });
                LoadRoles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"新增角色失败: {ex.Message}", "错误");
            }
        }
    }

    private async void BtnDeleteRole_Click(object sender, RoutedEventArgs e)
    {
        if (lbRoles.SelectedItem is not RoleItem role) { MessageBox.Show("请选择角色", "提示"); return; }
        if (MessageBox.Show($"确认删除角色 {role.Name}?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            await db.ExecuteAsync("DELETE FROM mnu WHERE code = @Code", new { Code = role.Id });
            LoadRoles();
            spPermissions.Children.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除角色失败: {ex.Message}", "错误");
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (lbRoles.SelectedItem is not RoleItem role) { MessageBox.Show("请选择角色", "提示"); return; }

        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var checkedPerms = _permCheckBoxes.Where(cb => cb.IsChecked == true).Select(cb => cb.Tag?.ToString());
            var authStr = string.Join(",", checkedPerms);
            await db.ExecuteAsync("UPDATE mnu SET auth = @Auth WHERE code = @Code",
                new { Auth = authStr, Code = role.Id });
            MessageBox.Show("权限保存成功", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误");
        }
    }
}
