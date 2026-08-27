using System;
using System.Collections.ObjectModel;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace QP11.Wpf.Views;

/// <summary>
/// 供应商选择器弹窗，支持搜索过滤
/// </summary>
public partial class SupplierSelectorWindow : Window
{
    private readonly ISupplierRepository _supplierRepo = App.ServiceProvider.GetRequiredService<ISupplierRepository>();
    public ObservableCollection<SupplierInfor> Suppliers { get; } = new();
    public SupplierInfor? SelectedSupplier { get; private set; }

    public SupplierSelectorWindow()
    {
        InitializeComponent();
        dgSuppliers.ItemsSource = Suppliers;
        LoadSuppliers();
    }

    /// <summary>
    /// 加载供应商列表，支持关键词过滤
    /// </summary>
    private async void LoadSuppliers(string? keyword = null)
    {
        try
        {
            Suppliers.Clear();
            var data = string.IsNullOrEmpty(keyword)
                ? await _supplierRepo.GetAllAsync()
                : await _supplierRepo.SearchAsync(keyword);
            foreach (var s in data) Suppliers.Add(s);
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载供应商失败"); MessageBox.Show($"加载供应商失败: {ex.Message}", "错误"); }
    }

    /// <summary>
    /// 搜索框文本变化时自动查询
    /// </summary>
    private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var kw = txtSearch.Text.Trim();
        if (kw.Length >= 1) LoadSuppliers(kw);
        else if (kw.Length == 0) LoadSuppliers();
    }

    /// <summary>
    /// 手动点击查询按钮
    /// </summary>
    private void BtnSearch_Click(object sender, RoutedEventArgs e) => LoadSuppliers(txtSearch.Text.Trim());

    /// <summary>
    /// 双击行选择供应商
    /// </summary>
    private void DgSuppliers_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => SelectAndClose();

    /// <summary>
    /// 选中供应商并关闭窗口
    /// </summary>
    private void SelectAndClose()
    {
        if (dgSuppliers.SelectedItem is not SupplierInfor supplier) return;
        SelectedSupplier = supplier;
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 确定选择按钮
    /// </summary>
    private void BtnConfirm_Click(object sender, RoutedEventArgs e) => SelectAndClose();
}
