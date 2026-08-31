using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

/// <summary>权限管理窗口：按新系统主窗口菜单为用户分配权限码，保存到 user_infor.auth。</summary>
public partial class RolePermissionWindow : Window
{
    private List<UserItem> _users = new();
    private string? _currentUsername;
    private bool _loadingMenu = true; // 防抖：程序设置勾选状态时不触发联动/保存
    private readonly HashSet<string> _allTreeCodes = new(StringComparer.OrdinalIgnoreCase); // 新菜单树内全部权限码

    public RolePermissionWindow()
    {
        InitializeComponent();
        LoadUsers();
        LoadMenuTree();
    }

    #region 数据加载

    private async void LoadUsers()
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            _users = (await db.QueryAsync<UserItem>(
                "SELECT u.username AS Username, u.name AS Name, g.name AS GroupName " +
                "FROM user_infor u LEFT JOIN groups g ON u.groups = g.id ORDER BY u.username")).ToList();
            lbUsers.ItemsSource = _users;

            // 尽量选中当前登录用户
            if (!string.IsNullOrEmpty(_currentUsername))
            {
                lbUsers.SelectedItem = _users.FirstOrDefault(u => string.Equals(u.Username, _currentUsername, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载用户失败: {ex.Message}", "错误");
        }
    }

    /// <summary>按新系统主窗口菜单构建权限树（顶层组映射父码 1/2/3/4/7，叶子取菜单数字 Tag）</summary>
    private void LoadMenuTree()
    {
        tvMenus.Items.Clear();
        _allTreeCodes.Clear();

        if (App.Current?.MainWindow is not MainWindow mw)
        {
            MessageBox.Show("无法获取主菜单，请从主窗口打开权限管理", "提示");
            return;
        }

        foreach (var root in mw.GetMenuPermissionTree())
            tvMenus.Items.Add(BuildItem(root));
        CollectTreeCodes(tvMenus.Items);
    }

    private void CollectTreeCodes(ItemCollection items)
    {
        foreach (var obj in items)
        {
            if (obj is TreeViewItem item && item.Tag is string code)
            {
                _allTreeCodes.Add(code);
                CollectTreeCodes(item.Items);
            }
        }
    }

    private TreeViewItem BuildItem(MenuNode node)
    {
        var check = new CheckBox
        {
            Content = string.IsNullOrEmpty(node.Code) ? CleanHeader(node.Name) : $"{node.Code}  {CleanHeader(node.Name)}",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2)
        };
        _loadingMenu = true;
        check.Checked += CheckBox_Changed;
        check.Unchecked += CheckBox_Changed;
        _loadingMenu = false;

        var item = new TreeViewItem
        {
            Tag = node.Code,
            Header = check
        };
        foreach (var child in node.Children)
            item.Items.Add(BuildItem(child));
        return item;
    }

    #endregion

    #region 勾选联动

    private void CheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingMenu || sender is not CheckBox cb) return;

        // 勾选/取消父节点时联动所有子节点
        if (TryFindParentTreeItem(cb, out var item))
        {
            var isChecked = cb.IsChecked == true;
            SetChildrenChecked(item, isChecked);
        }
    }

    private void SetChildrenChecked(TreeViewItem parent, bool isChecked)
    {
        foreach (var obj in parent.Items)
        {
            if (obj is not TreeViewItem child) continue;
            var childCb = child.Header as CheckBox;
            if (childCb == null) continue;
            _loadingMenu = true;
            childCb.IsChecked = isChecked;
            _loadingMenu = false;
            SetChildrenChecked(child, isChecked);
        }
    }

    private static bool TryFindParentTreeItem(DependencyObject child, out TreeViewItem item)
    {
        item = null!;
        var cur = child;
        while (cur != null)
        {
            if (cur is TreeViewItem tvi) { item = tvi; return true; }
            var parent = VisualTreeHelper.GetParent(cur);
            if (parent == null && cur is FrameworkElement fe && fe.Parent != null) parent = fe.Parent;
            cur = parent;
        }
        return false;
    }

    #endregion

    #region 用户选择 / 保存

    private void LbUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lbUsers.SelectedItem is not UserItem u) return;
        _currentUsername = u.Username;
        txtSelUser.Text = $"{u.Display}（{u.GroupName ?? "未分组"}）";
        LoadUserAuth(u.Username!);
    }

    private async void LoadUserAuth(string username)
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var row = await db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT auth, [groups] FROM user_infor WHERE username = @Username", new { Username = username });
            var auth = (string?)row?.auth ?? "";
            var userGroups = row?.groups != null ? Convert.ToInt32(row.groups) : (int?)null;
            var codes = auth.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 超管提示：auth 含 all 时菜单全部放行，需移除 all 勾选才能按勾选控制
            if (codes.Contains("all"))
                txtSelUser.Text += "　[超级管理员：all 完全放行，去掉 all 后勾选即生效]";
            // 降级按钮可用：用户是超管（auth 含 all）或其角色组为“超级管理”(3)
            btnDemote.IsEnabled = codes.Contains("all") || userGroups == 3;

            _loadingMenu = true;
            ApplyCheckedState(tvMenus.Items, codes);
            _loadingMenu = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载权限失败: {ex.Message}", "错误");
        }
    }

    private async Task<List<string>> LoadAuthCodes(string username)
    {
        var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var db = await dbFactory.CreateAsync();
        var row = await db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT auth FROM user_infor WHERE username = @Username", new { Username = username });
        var auth = (string?)row?.auth ?? "";
        return auth.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private void ApplyCheckedState(ItemCollection items, HashSet<string> codes)
    {
        foreach (var obj in items)
        {
            if (obj is not TreeViewItem item || item.Header is not CheckBox cb || item.Tag is not string code) continue;
            cb.IsChecked = codes.Contains(code) || codes.Any(c => code.StartsWith(c, StringComparison.OrdinalIgnoreCase));
            ApplyCheckedState(item.Items, codes);
        }
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e) => SetAllChecked(tvMenus.Items, true);

    private void BtnSelectNone_Click(object sender, RoutedEventArgs e) => SetAllChecked(tvMenus.Items, false);

    private void SetAllChecked(ItemCollection items, bool isChecked)
    {
        foreach (var obj in items)
        {
            if (obj is not TreeViewItem item) continue;
            if (item.Header is CheckBox cb)
            {
                _loadingMenu = true;
                cb.IsChecked = isChecked;
                _loadingMenu = false;
            }
            SetAllChecked(item.Items, isChecked);
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentUsername))
        {
            MessageBox.Show("请先选择用户", "提示");
            return;
        }

        try
        {
            // 保留新菜单树之外的已有权限码（如旧系统 5/6、13a2 等），避免保存覆盖丢失
            var existing = await LoadAuthCodes(_currentUsername);
            var preserved = existing.Where(c => !_allTreeCodes.Contains(c)).ToList();

            var checkedCodes = new List<string>();
            CollectChecked(tvMenus.Items, checkedCodes);

            // 精简为最小父集：去掉被更高层码覆盖的子码
            var minSet = checkedCodes
                .Where(c => !checkedCodes.Any(p =>
                    !string.Equals(p, c, StringComparison.OrdinalIgnoreCase) &&
                    c.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            var authStr = string.Join(",", preserved.Concat(minSet).OrderBy(c => c, StringComparer.Ordinal));

            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            await db.ExecuteAsync("UPDATE user_infor SET auth = @Auth WHERE username = @Username",
                new { Auth = authStr, Username = _currentUsername });

            // 若保存的是当前登录用户，立即刷新权限与界面（菜单/工作台按钮重新应用，无需重新登录）
            if (App.PermissionService != null &&
                string.Equals(App.CurrentUser?.Username, _currentUsername, StringComparison.OrdinalIgnoreCase))
            {
                await App.PermissionService.LoadUserPermissionsAsync(_currentUsername);
                (App.Current?.MainWindow as MainWindow)?.RefreshAllPermissionUi();
            }

            MessageBox.Show($"权限已保存：{authStr}", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误");
        }
    }

    private void CollectChecked(ItemCollection items, List<string> codes)
    {
        foreach (var obj in items)
        {
            if (obj is not TreeViewItem item) continue;
            if (item.Header is CheckBox cb && cb.IsChecked == true && item.Tag is string code)
                codes.Add(code);
            CollectChecked(item.Items, codes);
        }
    }

    /// <summary>把超管（auth 含 all）降级为普通管理员：移除 all（保留其它码），角色组改为管理员(groups=2)</summary>
    private async void BtnDemote_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentUsername)) return;
        if (MessageBox.Show(
                $"确认将用户 [{_currentUsername}] 调整为普通管理员？\n将移除其 all 超级权限（如有），并把角色组改为“管理员”（groups=2）。",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            var existing = await LoadAuthCodes(_currentUsername);
            var plain = existing.Where(c => !string.Equals(c, "all", StringComparison.OrdinalIgnoreCase)).ToList();
            var authStr = string.Join(",", plain);

            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            await db.ExecuteAsync("UPDATE user_infor SET auth = @Auth, [groups] = 2 WHERE username = @Username",
                new { Auth = authStr, Username = _currentUsername });

            // 若降级的是当前登录用户，立即刷新权限与界面
            if (App.PermissionService != null &&
                string.Equals(App.CurrentUser?.Username, _currentUsername, StringComparison.OrdinalIgnoreCase))
            {
                await App.PermissionService.LoadUserPermissionsAsync(_currentUsername);
                (App.Current?.MainWindow as MainWindow)?.RefreshAllPermissionUi();
            }

            LoadUsers();                  // 刷新用户列表（组别变化）
            LoadUserAuth(_currentUsername); // 刷新勾选状态与按钮可用性
            MessageBox.Show($"已降级为普通管理员，权限：{authStr}", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"操作失败: {ex.Message}", "错误");
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadUsers();
        LoadMenuTree();
    }

    private static string CleanHeader(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var idx = name.IndexOf('(');
        return idx > 0 ? name[..idx].Trim() : name;
    }

    #endregion
}

public class UserItem
{
    public string? Username { get; set; }
    public string? Name { get; set; }
    public string? GroupName { get; set; }
    public string Display => $"{Username}（{Name}）";
}

public class MenuNode
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public MenuNode? Parent { get; set; }
    public List<MenuNode> Children { get; } = new();
}