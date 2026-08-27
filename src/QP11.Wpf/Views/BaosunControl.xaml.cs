using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QP11.Core.Entities;
using QP11.Core.Models;
using QP11.Services;
using QP11.Wpf.ViewModels;

namespace QP11.Wpf.Views;

public class BaosunDetailItem : INotifyPropertyChanged
{
    public long? Partid { get; set; }
    public string? PartNo { get; set; }
    public string? PartName { get; set; }
    public string? Unit { get; set; }
    public string? Cartype { get; set; }
    public decimal? Cb { get; set; }
    public string? Memo { get; set; }

    private decimal _inprice;
    public decimal Inprice
    {
        get => _inprice;
        set { _inprice = value; OnPropertyChanged(nameof(Inprice)); OnPropertyChanged(nameof(SubTotal)); }
    }

    private decimal _amount = 1;
    public decimal Amount
    {
        get => _amount;
        set { _amount = value; OnPropertyChanged(nameof(Amount)); OnPropertyChanged(nameof(SubTotal)); }
    }

    public decimal SubTotal => Math.Round(Inprice * Amount, 2);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class BaosunControl : UserControl, ITabContent
{
    private readonly BaosunViewModel _viewModel;

    private bool _isQueryMode;
    private BillBaosun? _selectedBill;
    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _billDebounceCts;

    public ObservableCollection<BaosunDetailItem> Details => _viewModel.Details;
    public string TabTitle => "报损管理";
    public bool HasUnsavedChanges => Details.Count > 0;
    public event EventHandler? RequestClose;

    public BaosunControl(BaosunViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        dgDetails.ItemsSource = Details;
        dtBillDate.SelectedDate = DateTime.Now;
        dtQStart.SelectedDate = DateTime.Now.AddDays(-30);
        dtQEnd.SelectedDate = DateTime.Now;
        LoadDropdowns();
        LoadPartList();
    }

    private async void LoadDropdowns()
    {
        try
        {
            var users = await _viewModel.LoadUsersAsync();
            cboWorker.ItemsSource = users;
            cboWorker.DisplayMemberPath = "Name";
            cboWorker.SelectedValuePath = "Username";

            // 自动填充当前登录账号
            if (App.CurrentUser != null)
            {
                var currentUser = users.FirstOrDefault(u => u.Username == App.CurrentUser.Username);
                if (currentUser != null)
                    cboWorker.SelectedItem = currentUser;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "LoadDropdowns 失败");
        }
    }

    private async void LoadPartList()
    {
        try
        {
            var partNo = string.IsNullOrWhiteSpace(txtPartNo.Text) ? null : txtPartNo.Text.Trim();
            var partName = string.IsNullOrWhiteSpace(txtPartName.Text) ? null : txtPartName.Text.Trim();
            var cartype = string.IsNullOrWhiteSpace(txtCarType.Text) ? null : txtCarType.Text.Trim();
            var data = await _viewModel.LoadPartListAsync(partNo, partName, cartype);
            dgParts.ItemsSource = data;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载配件列表失败: {ex.Message}", "错误");
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        _ = DebounceSearchPartsAsync(token);
    }

    /// <summary>自动全选搜索框文本</summary>
    private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()));
        }
    }

    /// <summary>左右方向键切换搜索框</summary>
    private void TxtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;

        if (e.Key == Key.Left && tb.CaretIndex == 0)
        {
            // 光标在最左时按左键 → 跳到上一个输入框
            MoveFocusTo(tb, -1);
            e.Handled = true;
        }
        else if (e.Key == Key.Right && tb.CaretIndex == tb.Text.Length)
        {
            // 光标在最右时按右键 → 跳到下一个输入框
            MoveFocusTo(tb, 1);
            e.Handled = true;
        }
    }

    private static readonly TextBox?[] SearchBoxes = new TextBox?[3];

    private void MoveFocusTo(TextBox current, int direction)
    {
        // 懒加载搜索框引用
        if (SearchBoxes[0] == null)
        {
            SearchBoxes[0] = txtPartNo;
            SearchBoxes[1] = txtPartName;
            SearchBoxes[2] = txtCarType;
        }

        for (int i = 0; i < SearchBoxes.Length; i++)
        {
            if (SearchBoxes[i] == current)
            {
                int next = i + direction;
                if (next >= 0 && next < SearchBoxes.Length)
                {
                    SearchBoxes[next]?.Focus();
                }
                break;
            }
        }
    }

    private async Task DebounceSearchPartsAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);
            if (!token.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(LoadPartList);
            }
        }
        catch (TaskCanceledException) { }
    }

    private void DgParts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (dgParts.SelectedItem == null) return;
        try
        {
            if (dgParts.SelectedItem is not PartStockDisplay row) return;
            long partid = row.PartId;
            string partno = row.PartNo ?? "";
            string name = row.Name ?? "";
            decimal inprice = row.InPrice ?? 0m;

            var existing = Details.FirstOrDefault(d => d.Partid == partid);
            if (existing != null)
            {
                existing.Amount += 1;
            }
            else
            {
                Details.Add(new BaosunDetailItem
                {
                    Partid = partid,
                    PartNo = partno,
                    PartName = name,
                    Unit = row.Unit,
                    Cartype = row.CarType,
                    Cb = row.InPrice,
                    Inprice = inprice,
                    Amount = 1
                });
            }
            UpdateTotals();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "添加报损配件失败");
        }
    }

    private void UpdateTotals()
    {
        if (!IsLoaded) return;

        var total = Details.Sum(d => d.Inprice * d.Amount);
        txtSumTotal.Text = total.ToString("N2");
        txtSumAmount.Text = Details.Sum(d => d.Amount).ToString();
    }

    private void BtnSwitchMode_Click(object sender, RoutedEventArgs e) => ToggleMode();

    private void BtnQuery_Click(object sender, RoutedEventArgs e)
    {
        _billDebounceCts?.Cancel();
        LoadBills();
    }

    private void ToggleMode()
    {
        _isQueryMode = !_isQueryMode;
        if (_isQueryMode)
        {
            panelOrderMode.Visibility = Visibility.Collapsed;
            panelQueryMode.Visibility = Visibility.Visible;
        }
        else
        {
            panelOrderMode.Visibility = Visibility.Visible;
            panelQueryMode.Visibility = Visibility.Collapsed;
        }
    }

    #region 查询模式

    private void OnBillDateChanged(object sender, SelectionChangedEventArgs e)
    {
        _billDebounceCts?.Cancel();
        _billDebounceCts = new CancellationTokenSource();
        var token = _billDebounceCts.Token;
        _ = DebounceSearchBillsAsync(token);
    }

    private async Task DebounceSearchBillsAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);
            if (!token.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(LoadBills);
            }
        }
        catch (TaskCanceledException) { }
    }

    private async void LoadBills()
    {
        try
        {
            var data = (await _viewModel.LoadBillListAsync(dtQStart.SelectedDate, dtQEnd.SelectedDate)).ToList();
            var display = data.Select(b => new
            {
                b.Sn,
                b.Datetime,
                b.Worker,
                b.Total,
                b.Flag,
                b.Memo,
                FlagText = b.Flag switch
                {
                    0 => "草稿",
                    1 => "已审核",
                    3 => "已作废",
                    _ => "未知"
                }
            }).ToList();
            dgBills.ItemsSource = display;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询失败: {ex.Message}", "错误");
        }
    }

    private async void DgBills_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgBills.SelectedItem == null) return;
        try
        {
            dynamic row = dgBills.SelectedItem;
            string sn = row.Sn ?? "";
            if (string.IsNullOrEmpty(sn)) return;

            _selectedBill = await _viewModel.LoadBillAsync(sn);
            if (_selectedBill == null) return;

            var details = await _viewModel.LoadDetailsAsync(sn);
            dgQueryDetails.ItemsSource = details;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "DgBills_SelectionChanged 失败");
        }
    }

    #endregion

    #region ITabContent

    public void OnAdd()
    {
        if (_isQueryMode) { ToggleMode(); return; }
        txtPartNo.Focus();
        txtPartNo.SelectAll();
    }

    public void OnEdit()
    {
        if (_isQueryMode) return;
        if (dgDetails.SelectedItem is BaosunDetailItem item)
        {
            item.Amount += 1;
            UpdateTotals();
        }
    }

    public void OnQuery() => ToggleMode();

    public void OnDelete()
    {
        if (_isQueryMode) return;
        if (dgDetails.SelectedItem is BaosunDetailItem item)
        {
            Details.Remove(item);
            UpdateTotals();
        }
    }

    public void OnSave() => SaveBill();

    public void OnSettle() => SettleBill();

    public void OnPrint() { }

    public void OnReturn() { }

    public void OnCancel() => ClearBill();

    public void OnHistory() { }

    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);

    #endregion

    private async void SaveBill()
    {
        if (_isQueryMode) return;
        if (Details.Count == 0)
        {
            MessageBox.Show("请添加报损明细", "提示");
            return;
        }

        try
        {
            IsEnabled = false;

            var totalAmount = Details.Sum(d => d.Inprice * d.Amount);

            var bill = new BillBaosun
            {
                Worker = (cboWorker.SelectedItem as UserInfor)?.Name ?? cboWorker.Text.Trim(),
                Operator = App.CurrentUser?.Username,
                Total = totalAmount,
                Flag = 0,
                Memo = txtMemo.Text.Trim()
            };

            var billNo = await _viewModel.SaveBillAsync(bill, Details);
            txtBillNo.Text = billNo;

            MessageBox.Show($"报损单保存成功!\n单号: {billNo}\n合计: {totalAmount:N2}",
                "提示", MessageBoxButton.OK, MessageBoxImage.Information);

            ClearBill();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async void SettleBill()
    {
        if (_isQueryMode) return;
        if (Details.Count == 0) return;
        if (string.IsNullOrEmpty(txtBillNo.Text))
        {
            MessageBox.Show("请先保存单据", "提示");
            return;
        }

        try
        {
            await _viewModel.SettleBillAsync(txtBillNo.Text);
            MessageBox.Show("审核成功", "提示");
            ClearBill();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"审核失败: {ex.Message}", "错误");
        }
    }

    private void ClearBill()
    {
        Details.Clear();
        txtBillNo.Text = "";
        dtBillDate.SelectedDate = DateTime.Now;
        // 注意：操作员(cboWorker)不重置，避免结算完成后丢失选择
        txtMemo.Text = "";
        txtSumTotal.Text = "0.00";
        txtSumAmount.Text = "0";
    }
}
