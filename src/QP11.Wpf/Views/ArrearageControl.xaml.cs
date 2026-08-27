using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class ArrearageControl : UserControl, ITabContent
{
    private readonly IArrearageRepository _arrearageRepo;
    private readonly IFinanceService _financeService;
    private List<ArrearageDetailRow> _detailRows = new();

    private int _mode;
    private string _tabTitle;

    public string TabTitle => _tabTitle;
    public bool HasUnsavedChanges => false;
    public event EventHandler? RequestClose;

    public ArrearageControl(IArrearageRepository arrearageRepo, IFinanceService financeService, int mode = 1)
    {
        _arrearageRepo = arrearageRepo;
        _financeService = financeService;
        InitializeComponent();
        _mode = mode;
        _tabTitle = mode switch
        {
            1 => "应付款",
            2 => "应收款",
            _ => "欠款管理"
        };
        cboMode.SelectedIndex = mode - 1;
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadClientsAsync();
    }

    private async System.Threading.Tasks.Task LoadClientsAsync()
    {
        try
        {
            var keyword = txtSearch.Text.Trim();
            var data = (await _arrearageRepo.GetClientArrearageListAsync(_mode, keyword)).ToList();
            dgClients.ItemsSource = data;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载客户列表失败: {ex.Message}", "错误");
        }
    }

    private object? GetSelectedClient()
    {
        return dgClients.SelectedItem;
    }

    private async void DgClients_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var sel = GetSelectedClient();
        if (sel == null) return;
        dynamic row = sel;
        var bid = (string?)row.bid;
        if (string.IsNullOrEmpty(bid)) return;

        await LoadDetailAsync(bid);
    }

    private async System.Threading.Tasks.Task LoadDetailAsync(string bid)
    {
        try
        {
            var data = (await _arrearageRepo.GetArrearageDetailByBidAsync(bid, _mode)).ToList();
            _detailRows = data.Select(d => new ArrearageDetailRow
            {
                Id = (long)d.id,
                Sn = (string?)d.sn,
                Datetime = (DateTime?)d.datetime,
                Je = d.je == null ? 0m : Convert.ToDecimal(d.je),
                Charge = d.charge == null ? 0m : Convert.ToDecimal(d.charge),
                Owe = d.owe == null ? 0m : Convert.ToDecimal(d.owe),
                IsReturn = Convert.ToInt32(d.is_return) == 1,
                PayAmount = 0,
                IsSelected = false
            }).ToList();
            dgDetail.ItemsSource = new BindingList<ArrearageDetailRow>(_detailRows);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载明细失败: {ex.Message}", "错误");
        }
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => _ = LoadClientsAsync();

    private void DgDetail_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is ArrearageDetailRow row && row.IsReturn)
            e.Row.Foreground = System.Windows.Media.Brushes.Red;
    }

    private void CboMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (cboMode.SelectedItem is not ComboBoxItem item) return;
        _mode = int.Parse((string)item.Tag);
        _tabTitle = _mode switch
        {
            1 => "应付款",
            2 => "应收款",
            _ => "欠款管理"
        };
        _ = LoadClientsAsync();
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        if (_detailRows.Count == 0)
        {
            MessageBox.Show("没有可打印的数据", "提示");
            return;
        }

        try
        {
            var clientName = "";
            var clientItem = dgClients.SelectedItem;
            if (clientItem != null)
            {
                var nameProp = clientItem.GetType().GetProperty("name");
                if (nameProp != null) clientName = nameProp.GetValue(clientItem)?.ToString() ?? "";
            }

            var modeText = cboMode.SelectedIndex switch
            {
                0 => "应付款",
                1 => "应收款",
                _ => "欠款"
            };

            var dt = new System.Data.DataTable($"{clientName}{modeText}对账单");
            dt.Columns.Add("单号", typeof(string));
            dt.Columns.Add("日期", typeof(string));
            dt.Columns.Add("欠款", typeof(decimal));
            dt.Columns.Add("已付", typeof(decimal));
            dt.Columns.Add("未付", typeof(decimal));

            foreach (var row in _detailRows)
            {
                dt.Rows.Add(row.Sn, row.Datetime?.ToString("yyyy-MM-dd"), row.Je, row.Charge, row.Owe);
            }

            // 合计行
            dt.Rows.Add("合计", "", _detailRows.Sum(r => r.Je), _detailRows.Sum(r => r.Charge), _detailRows.Sum(r => r.Owe));

            var dlg = new PrintPreviewWindow(dt, $"{clientName}{modeText}对账单")
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打印失败: {ex.Message}", "错误");
        }
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _detailRows)
            row.IsSelected = true;
        dgDetail.ItemsSource = new BindingList<ArrearageDetailRow>(_detailRows);
    }

    private async void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        var selected = _detailRows.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("请选择要确认到账的记录", "提示");
            return;
        }

        // 自动填入未付余额
        foreach (var row in selected)
        {
            if (row.PayAmount <= 0)
                row.PayAmount = row.Owe;
        }

        var totalPay = selected.Sum(r => r.PayAmount);

        var dlg = new ConfirmPaymentDialog(selected.Count, totalPay) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var payments = selected.Select(r => (r.Id, r.PayAmount));
            await _financeService.ConfirmArrearagePaymentAsync(
                dlg.Amount,
                payments,
                dlg.PayMethod);

            MessageBox.Show("确认到账成功", "提示");

            // 刷新数据
            var sel = GetSelectedClient();
            if (sel != null)
            {
                dynamic client = sel;
                var bid = (string?)client.bid;
                if (!string.IsNullOrEmpty(bid)) await LoadDetailAsync(bid);
            }
            await LoadClientsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"确认到账失败: {ex.Message}", "错误");
        }
    }

    #region ITabContent

    public void OnAdd() { }
    public void OnEdit() { }
    public async void OnQuery() => await LoadClientsAsync();
    public void OnDelete() { }
    public void OnSave() { }
    public void OnSettle() => BtnConfirm_Click(this, new RoutedEventArgs());
    public void OnPrint() => BtnPrint_Click(this, new RoutedEventArgs());
    public void OnReturn() { }
    public void OnCancel() { }
    public void OnHistory() { }
    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);

    #endregion
}

public class ArrearageDetailRow : INotifyPropertyChanged
{
    public long Id { get; set; }
    public string? Sn { get; set; }
    public DateTime? Datetime { get; set; }
    public decimal Je { get; set; }
    public decimal Charge { get; set; }
    public decimal Owe { get; set; }
    public bool IsReturn { get; set; }

    private decimal _payAmount;
    public decimal PayAmount
    {
        get => _payAmount;
        set { _payAmount = value; OnPropertyChanged(nameof(PayAmount)); }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
