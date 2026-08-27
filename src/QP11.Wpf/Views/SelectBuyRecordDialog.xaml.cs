using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QP11.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace QP11.Wpf.Views;

/// <summary>
/// 进货记录选择项
/// </summary>
public class BuyRecordItem : INotifyPropertyChanged
{
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
    }

    public string? Sn { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierSid { get; set; }
    public long Amount { get; set; }
    public decimal InPrice { get; set; }
    public decimal InTotal { get; set; }
    public DateTime? Datetime { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 选择进货记录对话框：销售退货勾选废品仓后弹出，让用户选择关联的进货记录
/// </summary>
public partial class SelectBuyRecordDialog : Window
{
    private readonly IBuyRepository _buyRepo = App.ServiceProvider.GetRequiredService<IBuyRepository>();
    private readonly long _partId;

    /// <summary>用户是否确认选择了进货记录</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>选中的采购单号</summary>
    public string? SelectedBuySn { get; private set; }

    /// <summary>选中的供应商名称</summary>
    public string? SelectedSupplierName { get; private set; }

    /// <summary>选中的供应商ID</summary>
    public string? SelectedSupplierSid { get; private set; }

    /// <summary>选中的进价</summary>
    public decimal SelectedInPrice { get; private set; }

    public SelectBuyRecordDialog(long partId, string partNo, string partName, int returnAmount)
    {
        InitializeComponent();

        _partId = partId;
        txtPartNo.Text = partNo;
        txtPartName.Text = partName;
        txtReturnAmount.Text = returnAmount.ToString();

        Loaded += async (_, _) => await LoadBuyRecordsAsync();
    }

    private async System.Threading.Tasks.Task LoadBuyRecordsAsync()
    {
        try
        {
            var records = await _buyRepo.GetBuyDetailsByPartIdAsync(_partId);
            var items = records.Select(r => new BuyRecordItem
            {
                Sn = (string?)r.sn ?? "",
                SupplierName = (string?)r.supplier_name ?? "",
                SupplierSid = (string?)r.supplier_sid ?? "",
                Amount = (long)(r.amount ?? 0),
                InPrice = (decimal)(r.inprice ?? 0),
                InTotal = (decimal)(r.intotal ?? 0),
                Datetime = r.datetime as DateTime?
            }).ToList();

            dgBuyRecords.ItemsSource = items;

            if (items.Count == 0)
            {
                MessageBox.Show("未找到该配件的进货记录，将按废品仓处理", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载进货记录失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DgBuyRecords_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgBuyRecords.SelectedItem is BuyRecordItem item)
        {
            // 选中行时自动勾选，取消其他行的勾选
            foreach (var row in (System.Collections.IList)dgBuyRecords.ItemsSource)
            {
                if (row is BuyRecordItem ri)
                    ri.IsSelected = (ri == item);
            }
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        var selected = ((System.Collections.Generic.List<BuyRecordItem>?)dgBuyRecords.ItemsSource)
            ?.FirstOrDefault(r => r.IsSelected);

        if (selected == null)
        {
            MessageBox.Show("请选择一条进货记录", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsConfirmed = true;
        SelectedBuySn = selected.Sn;
        SelectedSupplierName = selected.SupplierName;
        SelectedSupplierSid = selected.SupplierSid;
        SelectedInPrice = selected.InPrice;

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        // 取消 → 不选进货记录，走原来的废品仓逻辑
        IsConfirmed = false;
        DialogResult = false;
        Close();
    }
}
