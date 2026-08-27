using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Data;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using QP11.Wpf.Helpers;

namespace QP11.Wpf.Views;

/// <summary>
/// 客户选择器弹窗，支持拼音搜索和欠款提示
/// </summary>
public partial class ClientSelectorWindow : Window
{
    private readonly IClientRepository _clientRepo = App.ServiceProvider.GetRequiredService<IClientRepository>();
    private readonly IArrearageRepository _arrearageRepo = App.ServiceProvider.GetRequiredService<IArrearageRepository>();
    public ObservableCollection<ClientInfor> Clients { get; } = new();
    public ClientInfor? SelectedClient { get; private set; }

    private List<ClientInfor> _allClients = new();
    private Dictionary<string, string> _clientPyCache = new();
    private System.ComponentModel.ICollectionView? _clientView;
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private string _pendingSearchText = "";

    public ClientSelectorWindow()
    {
        InitializeComponent();
        dgClients.ItemsSource = Clients;
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); FilterClientsCore(_pendingSearchText); };
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
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载客户失败"); MessageBox.Show($"加载客户失败: {ex.Message}", "错误"); }
    }

    /// <summary>
    /// 搜索框文本变化时防抖过滤
    /// </summary>
    private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _pendingSearchText = txtSearch.Text.Trim();
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    /// <summary>
    /// 内存过滤客户列表
    /// </summary>
    private void FilterClientsCore(string keyword)
    {
        try
        {
            if (_clientView == null) return;
            if (string.IsNullOrWhiteSpace(keyword))
            {
                _clientView.Filter = null;
                return;
            }
            var keywordLower = keyword.ToLower();
            _clientView.Filter = obj =>
            {
                if (obj is not ClientInfor c) return false;
                if (c.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) return true;
                if (c.Cid?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) return true;
                if (c.Mobile?.Contains(keyword) == true) return true;
                if (c.Tel?.Contains(keyword) == true) return true;
                if (_clientPyCache.TryGetValue(c.Cid ?? "", out var py) && py.StartsWith(keywordLower, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(c.NamePy) && c.NamePy.StartsWith(keywordLower, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            };
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "筛选客户失败");
        }
        finally { }
    }

    /// <summary>
    /// 手动点击查询按钮
    /// </summary>
    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        _pendingSearchText = txtSearch.Text.Trim();
        _searchTimer.Stop();
        FilterClientsCore(_pendingSearchText);
    }

    /// <summary>
    /// 双击行选择客户
    /// </summary>
    private async void DgClients_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        await SelectAndClose();
    }

    /// <summary>
    /// 选中客户并关闭窗口，同时查询欠款信息
    /// </summary>
    private async System.Threading.Tasks.Task SelectAndClose()
    {
        if (dgClients.SelectedItem is not ClientInfor client) return;
        SelectedClient = client;

        var arrearTotal = await _arrearageRepo.GetClientArrearTotalAsync(client.Cid!);
        if (arrearTotal > 0)
            txtArrearInfo.Text = $"⚠ 该客户欠款: {arrearTotal:C2}";
        else
            txtArrearInfo.Text = "";

        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 确定选择按钮
    /// </summary>
    private async void BtnConfirm_Click(object sender, RoutedEventArgs e) => await SelectAndClose();
}
