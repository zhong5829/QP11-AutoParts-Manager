using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QP11.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using QP11.Wpf.ViewModels;

namespace QP11.Wpf.Views;

public partial class BuyQueryControl : UserControl, ITabContent
{
    private readonly BuyQueryViewModel _viewModel;
    private readonly ISupplierRepository _supplierRepo = App.ServiceProvider.GetRequiredService<ISupplierRepository>();

    public string TabTitle => "采购单据查询";
    public bool HasUnsavedChanges => false;
    public event EventHandler? RequestClose;

    public BuyQueryControl(BuyQueryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        dtStart.SelectedDate = DateTime.Now.AddDays(-30);
        dtEnd.SelectedDate = DateTime.Now;
        Loaded += async (_, _) => await LoadSuppliersAsync();
        LoadBills();
    }

    private async System.Threading.Tasks.Task LoadSuppliersAsync()
    {
        try
        {
            var suppliers = (await _supplierRepo.GetAllAsync()).ToList();
            txtSupplier.SetSuppliers(suppliers);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载供应商列表失败");
        }
    }

    private async void LoadBills()
    {
        try
        {
            var data = (await _viewModel.LoadBillListAsync(
                dtStart.SelectedDate, dtEnd.SelectedDate,
                txtSupplier.SearchText.Trim(), txtWorker.Text.Trim())).ToList();

            dgBills.ItemsSource = data;

            txtCount.Text = $"单据数: {data.Count}";
            txtSumTotal.Text = data.Sum(r => (decimal)(r.total == null ? 0m : (decimal)r.total)).ToString("N2");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询失败: {ex.Message}", "错误");
        }
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => LoadBills();

    private void DgBills_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (dgBills.SelectedItem == null) return;
        dynamic row = dgBills.SelectedItem;
        string? sn;
        try { sn = (string?)row.sn; } catch { return; }
        if (string.IsNullOrEmpty(sn)) return;
        var win = new BuyEditWindow(sn);
        win.Owner = Window.GetWindow(this);
        win.Show();
    }

    private async void BtnVoid_Click(object sender, RoutedEventArgs e)
    {
        if (dgBills.SelectedItem == null) return;
        dynamic row = dgBills.SelectedItem;
        string? sn;
        try { sn = (string?)row.sn; } catch { return; }
        if (string.IsNullOrEmpty(sn)) return;
        if (MessageBox.Show($"确定作废单据 [{sn}]?", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            await _viewModel.VoidBillAsync(sn);
            LoadBills();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"作废失败: {ex.Message}", "错误");
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        dgBills.SelectAllCells();
        dgBills.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
        ApplicationCommands.Copy.Execute(null, dgBills);
        dgBills.UnselectAllCells();
        MessageBox.Show("数据已复制到剪贴板，可粘贴到Excel", "提示");
    }

    public void OnAdd() { }
    public void OnEdit() { }
    public void OnQuery() => LoadBills();
    public void OnDelete() { }
    public void OnSave() { }
    public void OnSettle() { }
    public void OnPrint() { }
    public void OnReturn() { }
    public void OnCancel() { }
    public void OnHistory() { }
    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);
}
