using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Wpf.Helpers;

namespace QP11.Wpf.Views;

/// <summary>
/// 客户管理窗口，提供客户的增删改查功能
/// </summary>
public partial class ClientManagerWindow : Window
{
    private readonly IClientRepository _clientRepo;
    public ObservableCollection<ClientInfor> Clients { get; } = new();

    private List<ClientInfor> _allClients = new();
    private Dictionary<string, string> _clientPyCache = new();
    private System.ComponentModel.ICollectionView? _clientView;
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    public ClientManagerWindow(IClientRepository clientRepo)
    {
        _clientRepo = clientRepo;
        InitializeComponent();
        dgClients.ItemsSource = Clients;
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); FilterClientsCore(); };
        LoadClients();
    }

    /// <summary>
    /// 加载客户列表（全量加载到内存）
    /// </summary>
    private async void LoadClients()
    {
        try
        {
            _allClients = (await _clientRepo.GetAllAsync()).ToList();
            _clientPyCache = _allClients.Where(c => !string.IsNullOrEmpty(c.Name))
                .ToDictionary(c => c.Cid ?? "", c => PinyinHelper.GetPinyinInitials(c.Name!));
            Clients.Clear();
            foreach (var c in _allClients) Clients.Add(c);
            _clientView = new CollectionViewSource { Source = Clients }.View;
            dgClients.ItemsSource = _clientView;
            txtCount.Text = $"共 {Clients.Count} 条记录";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载客户失败: {ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 搜索框文本变化时防抖过滤
    /// </summary>
    private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    /// <summary>
    /// 内存过滤客户列表（按名称、地址、电话分别匹配）
    /// </summary>
    private void FilterClientsCore()
    {
        try
        {
            if (_clientView == null) return;
            var name = txtName.Text.Trim();
            var address = txtAddress.Text.Trim();
            var tel = txtTel.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(tel))
            {
                _clientView.Filter = null;
                txtCount.Text = $"共 {Clients.Count} 条记录";
                return;
            }

            var nameLower = name.ToLower();
            _clientView.Filter = obj =>
            {
                if (obj is not ClientInfor c) return false;

                // 名称匹配（支持拼音首字母）
                if (!string.IsNullOrEmpty(name))
                {
                    bool nameMatch = c.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true;
                    if (!nameMatch)
                        nameMatch = _clientPyCache.TryGetValue(c.Cid ?? "", out var py) && py.StartsWith(nameLower, StringComparison.OrdinalIgnoreCase);
                    if (!nameMatch && !string.IsNullOrEmpty(c.NamePy))
                        nameMatch = c.NamePy.StartsWith(nameLower, StringComparison.OrdinalIgnoreCase);
                    if (!nameMatch) return false;
                }
                // 地址匹配
                if (!string.IsNullOrEmpty(address))
                {
                    if (c.Address?.Contains(address, StringComparison.OrdinalIgnoreCase) != true) return false;
                }
                // 电话匹配
                if (!string.IsNullOrEmpty(tel))
                {
                    if ((c.Tel?.Contains(tel, StringComparison.OrdinalIgnoreCase) != true)
                        && (c.Mobile?.Contains(tel) != true)) return false;
                }
                return true;
            };
            txtCount.Text = $"共 {_clientView.Cast<object>().Count()} 条记录";
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "筛选客户失败");
        }
        finally { }
    }

    /// <summary>
    /// 查询按钮点击
    /// </summary>
    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        _searchTimer.Stop();
        FilterClientsCore();
    }

    /// <summary>
    /// 新增客户
    /// </summary>
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var dlg = new ClientEditWindow();
        if (owner != null && owner.IsLoaded) dlg.Owner = owner;
        if (dlg.ShowDialog() == true) LoadClients();
    }

    /// <summary>
    /// 编辑选中的客户
    /// </summary>
    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (dgClients.SelectedItem is not ClientInfor client)
        {
            MessageBox.Show("请选择要编辑的客户", "提示");
            return;
        }
        var owner = Window.GetWindow(this);
        var dlg = new ClientEditWindow(client);
        if (owner != null && owner.IsLoaded) dlg.Owner = owner;
        if (dlg.ShowDialog() == true) LoadClients();
    }

    /// <summary>
    /// 逻辑删除选中的客户
    /// </summary>
    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (dgClients.SelectedItem is not ClientInfor client) return;
        if (MessageBox.Show($"确定删除客户 [{client.Name}]?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            await db.ExecuteAsync("DELETE FROM client_infor WHERE cid=@Cid", new { Cid = client.Cid! });
            LoadClients();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除失败: {ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 双击客户行进入编辑
    /// </summary>
    private void DgClients_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnEdit_Click(sender, e);
    }
}
