using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Wpf.Helpers;

namespace QP11.Wpf.Views;

/// <summary>
/// 供应商管理窗口，提供供应商的增删改查功能
/// </summary>
public partial class SupplierManagerWindow : Window
{
    private readonly ISupplierRepository _supplierRepo;
    public ObservableCollection<SupplierInfor> Suppliers { get; } = new();

    private List<SupplierInfor> _allSuppliers = new();
    private Dictionary<string, string> _supplierPyCache = new();
    private ICollectionView? _supplierView;
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    public SupplierManagerWindow(ISupplierRepository supplierRepo)
    {
        _supplierRepo = supplierRepo;
        InitializeComponent();
        dgSuppliers.ItemsSource = Suppliers;
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); FilterSuppliers(); };
        LoadSuppliers();
    }

    /// <summary>加载供应商列表（全量加载到内存）</summary>
    private async void LoadSuppliers()
    {
        try
        {
            _allSuppliers = (await _supplierRepo.GetAllAsync()).ToList();
            _supplierPyCache = _allSuppliers.Where(s => !string.IsNullOrEmpty(s.Name))
                .ToDictionary(s => s.Sid ?? "", s => PinyinHelper.GetPinyinInitials(s.Name!));
            Suppliers.Clear();
            foreach (var s in _allSuppliers) Suppliers.Add(s);
            _supplierView = new CollectionViewSource { Source = Suppliers }.View;
            dgSuppliers.ItemsSource = _supplierView;
            txtCount.Text = $"共 {Suppliers.Count} 条记录";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载供应商失败: {ex.Message}", "错误");
        }
    }

    /// <summary>搜索框文本变化时防抖过滤</summary>
    private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    /// <summary>内存过滤（按名称、地址、电话分别匹配）</summary>
    private void FilterSuppliers()
    {
        try
        {
            if (_supplierView == null) return;
            var name = txtName.Text.Trim();
            var address = txtAddress.Text.Trim();
            var tel = txtTel.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(tel))
            {
                _supplierView.Filter = null;
                txtCount.Text = $"共 {Suppliers.Count} 条记录";
                return;
            }

            var nameLower = name.ToLower();
            _supplierView.Filter = obj =>
            {
                if (obj is not SupplierInfor s) return false;
                // 名称匹配（支持拼音首字母）
                if (!string.IsNullOrEmpty(name))
                {
                    bool nameMatch = s.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true;
                    if (!nameMatch)
                        nameMatch = _supplierPyCache.TryGetValue(s.Sid ?? "", out var py) && py.StartsWith(nameLower, StringComparison.OrdinalIgnoreCase);
                    if (!nameMatch) return false;
                }
                if (!string.IsNullOrEmpty(address) && (s.Address?.Contains(address, StringComparison.OrdinalIgnoreCase) != true)) return false;
                if (!string.IsNullOrEmpty(tel))
                {
                    if ((s.Tel?.Contains(tel, StringComparison.OrdinalIgnoreCase) != true)
                        && (s.Mobile?.Contains(tel) != true)) return false;
                }
                return true;
            };
            txtCount.Text = $"共 {_supplierView.Cast<object>().Count()} 条记录";
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "筛选供应商失败");
        }
        finally { }
    }

    /// <summary>查询按钮点击</summary>
    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        _searchTimer.Stop();
        FilterSuppliers();
    }

    /// <summary>新增供应商</summary>
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var dlg = new SupplierEditWindow();
        if (owner != null && owner.IsLoaded) dlg.Owner = owner;
        if (dlg.ShowDialog() == true) LoadSuppliers();
    }

    /// <summary>编辑选中的供应商</summary>
    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is not SupplierInfor supplier)
        {
            MessageBox.Show("请选择要编辑的供应商", "提示");
            return;
        }
        var owner = Window.GetWindow(this);
        var dlg = new SupplierEditWindow(supplier);
        if (owner != null && owner.IsLoaded) dlg.Owner = owner;
        if (dlg.ShowDialog() == true) LoadSuppliers();
    }

    /// <summary>逻辑删除选中的供应商</summary>
    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is not SupplierInfor supplier) return;
        if (MessageBox.Show($"确定删除供应商 [{supplier.Name}]?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            await db.ExecuteAsync("DELETE FROM supplier_infor WHERE sid=@Sid", new { Sid = supplier.Sid! });
            LoadSuppliers();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除失败: {ex.Message}", "错误");
        }
    }

    /// <summary>双击进入编辑</summary>
    private void DgSuppliers_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnEdit_Click(sender, e);
    }
}
