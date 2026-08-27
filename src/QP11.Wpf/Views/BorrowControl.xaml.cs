using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Services;
using QP11.Wpf.ViewModels;

namespace QP11.Wpf.Views;

public class BorrowDetailItem : INotifyPropertyChanged
{
    public long? Partid { get; set; }
    public string? PartNo { get; set; }
    public string? PartName { get; set; }

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

public partial class BorrowControl : UserControl, ITabContent
{
    private readonly BorrowViewModel _viewModel;

    private int _currentFlag = 3;
    private BillBuy? _selectedBill;
    private bool _isNewBill = true;

    private Dictionary<string, string> _supplierNameMap = new();
    private Dictionary<string, string> _workerNameMap = new();
    private List<SupplierInfor> _allSuppliers = new();
    private List<UserInfor> _allUsers = new();

    public ObservableCollection<BorrowDetailItem> Details => _viewModel.Details;
    public string TabTitle => "借还管理";
    public bool HasUnsavedChanges => Details.Count > 0;
    public event EventHandler? RequestClose;

    public BorrowControl(BorrowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        dgDetails.ItemsSource = Details;
        dtBillDate.SelectedDate = DateTime.Now;
        LoadDropdowns();
        LoadBillList();
    }

    private async void LoadDropdowns()
    {
        try
        {
            var suppliers = await _viewModel.LoadSuppliersAsync();
            _allSuppliers = suppliers;
            cboSupplier.SetSuppliers(suppliers);
            _supplierNameMap = suppliers
                .Where(s => s.Sid != null && s.Name != null)
                .ToDictionary(s => s.Sid!, s => s.Name!);

            var users = await _viewModel.LoadUsersAsync();
            _allUsers = users.ToList();
            cboWorker.ItemsSource = _allUsers;
            cboWorker.DisplayMemberPath = "Name";
            cboWorker.SelectedValuePath = "Username";
            _workerNameMap = _allUsers
                .Where(u => u.Username != null && u.Name != null)
                .ToDictionary(u => u.Username!, u => u.Name!);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "LoadDropdowns 失败");
        }
    }

    private async void LoadBillList()
    {
        try
        {
            var data = (await _viewModel.LoadBillListAsync()).Where(b => b.Flag == _currentFlag).ToList();
            var display = data.Select(b => new
            {
                b.Sn,
                b.Datetime,
                Supplier = _supplierNameMap.TryGetValue(b.Supplier ?? "", out var sn) ? sn : b.Supplier,
                Worker = _workerNameMap.TryGetValue(b.Worker ?? "", out var wn) ? wn : b.Worker,
                b.Total,
                b.Flag,
                FlagText = _currentFlag == 3 ? "在借" : "已还"
            }).ToList();
            dgBills.ItemsSource = display;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载单据列表失败: {ex.Message}", "错误");
        }
    }

    private void RadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (rbBorrow.IsChecked == true)
            _currentFlag = 3;
        else if (rbReturn.IsChecked == true)
            _currentFlag = 4;
        LoadBillList();
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

            _isNewBill = false;
            txtBillNo.Text = _selectedBill.Sn;
            dtBillDate.SelectedDate = _selectedBill.Datetime ?? DateTime.Now;

            // 通过供应商名称查找并设置，避免显示数字ID
            var supplier = _allSuppliers.FirstOrDefault(s => s.Sid == _selectedBill.Supplier);
            cboSupplier.SetSupplier(supplier);

            // 通过采购员名称查找并设置，避免显示用户名
            var user = _allUsers.FirstOrDefault(u => u.Username == _selectedBill.Worker);
            cboWorker.Text = user?.Name ?? _selectedBill.Worker ?? "";

            txtMemo.Text = _selectedBill.Memo ?? "";
            txtTotal.Text = (_selectedBill.Total ?? 0m).ToString("N2");

            var details = await _viewModel.LoadDetailsAsync(sn);
            Details.Clear();
            foreach (var d in details)
            {
                Details.Add(new BorrowDetailItem
                {
                    Partid = d.Partid,
                    PartNo = d.Partno,
                    PartName = d.Name,
                    Amount = d.Amount ?? 1,
                    Inprice = d.Inprice ?? 0
                });
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载借出单明细失败");
        }
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        // 已还模式下不允许新增借货单
        if (_currentFlag == 4)
        {
            MessageBox.Show("已还模式下不能新增借货单，请切换到在借模式", "提示");
            return;
        }

        if (string.IsNullOrWhiteSpace(cboSupplier.SearchText.Trim()))
        {
            MessageBox.Show("请先选择供应商", "提示");
            cboSupplier.Focus();
            return;
        }

        // 复用配件选择窗口，启用借货模式
        var selector = new PartSelectorWindow(
            App.ServiceProvider.GetRequiredService<IPartRepository>(),
            App.ServiceProvider.GetRequiredService<IPartQueryService>())
        {
            BorrowMode = true,
            ExistingPartAmounts = Details.Where(d => d.Partid.HasValue).ToDictionary(d => d.Partid!.Value, d => d.Amount)
        };

        // 双击配件时实时添加到借货明细（窗口保持打开，可连续添加）
        selector.ItemAdded += result =>
        {
            var existing = Details.FirstOrDefault(d => d.Partid == result.PartId);
            if (existing != null)
            {
                existing.Amount += result.Amount;
                existing.Inprice = result.InPrice;
            }
            else
            {
                Details.Add(new BorrowDetailItem
                {
                    Partid = result.PartId,
                    PartNo = result.PartNo,
                    PartName = result.PartName,
                    Amount = result.Amount,
                    Inprice = result.InPrice
                });
            }
            UpdateTotals();
        };

        var owner = Window.GetWindow(this);
        if (owner != null && owner.IsLoaded)
            selector.Owner = owner;

        selector.ShowDialog();
    }

    /// <summary>
    /// 归还按钮：对选中的在借单(flag=3)生成一张 flag=4 负向还货单并扣减库存
    /// </summary>
    private async void BtnReturn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBill == null || string.IsNullOrEmpty(_selectedBill.Sn))
        {
            MessageBox.Show("请选择要归还的借货单", "提示");
            return;
        }

        if (_selectedBill.Flag != 3)
        {
            MessageBox.Show("只能归还状态为'在借'的单据", "提示");
            return;
        }

        if (MessageBox.Show($"确认归还借货单 [{_selectedBill.Sn}]?\n将生成还货单并扣减库存。",
            "确认归还", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            IsEnabled = false;
            var returnSn = await _viewModel.SaveReturnAsync(_selectedBill);
            MessageBox.Show($"归还成功!\n还货单号: {returnSn}", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            LoadBillList();
            OnCancel();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"归还失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void UpdateTotals()
    {
        if (!IsLoaded) return;

        var total = Details.Sum(d => d.Inprice * d.Amount);
        txtTotal.Text = total.ToString("N2");
    }

    #region ITabContent

    public void OnAdd()
    {
        _isNewBill = true;
        _selectedBill = null;
        Details.Clear();
        txtBillNo.Text = "";
        dtBillDate.SelectedDate = DateTime.Now;
        cboSupplier.ClearSelection();
        // 注意：采购员(cboWorker)不重置，避免结算/归还完成后丢失选择
        txtMemo.Text = "";
        txtTotal.Text = "0.00";
        cboSupplier.Focus();
    }

    public void OnEdit()
    {
        if (dgDetails.SelectedItem is BorrowDetailItem item)
        {
            item.Amount += 1;
            UpdateTotals();
        }
    }

    public void OnQuery() { }

    public void OnDelete()
    {
        if (dgDetails.SelectedItem is BorrowDetailItem item)
        {
            Details.Remove(item);
            UpdateTotals();
        }
    }

    public void OnSave() => SaveBill();

    public void OnSettle() => SettleBill();

    public void OnPrint() { }

    public void OnReturn()
    {
        rbReturn.IsChecked = true;
    }

    public void OnCancel()
    {
        Details.Clear();
        txtBillNo.Text = "";
        dtBillDate.SelectedDate = DateTime.Now;
        cboSupplier.ClearSelection();
        // 注意：采购员(cboWorker)不重置，避免结算/归还完成后丢失选择
        txtMemo.Text = "";
        txtTotal.Text = "0.00";
        _isNewBill = true;
        _selectedBill = null;
    }

    public void OnHistory() { }

    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);

    #endregion

    private async void SaveBill()
    {
        // 已还模式只读，不允许直接保存（归还只能通过归还按钮生成还货单）
        if (_currentFlag == 4)
        {
            MessageBox.Show("已还模式只读，归还请使用「归还」按钮", "提示");
            return;
        }

        if (Details.Count == 0)
        {
            MessageBox.Show("请添加借货明细", "提示");
            return;
        }

        var supplierId = cboSupplier.SelectedSupplierId ?? cboSupplier.SearchText.Trim();
        if (string.IsNullOrEmpty(supplierId))
        {
            MessageBox.Show("请选择供应商", "提示");
            cboSupplier.Focus();
            return;
        }

        try
        {
            IsEnabled = false;

            if (_isNewBill)
            {
                var totalAmount = Details.Sum(d => d.Inprice * d.Amount);

                var bill = new BillBuy
                {
                    Supplier = supplierId,
                    Worker = cboWorker.SelectedValue?.ToString() ?? cboWorker.Text.Trim(),
                    Total = totalAmount,
                    Cash = 0,
                    Flag = _currentFlag,
                    Memo = txtMemo.Text.Trim()
                };

                var billNo = await _viewModel.SaveNewBillAsync(bill, Details);
                txtBillNo.Text = billNo;

                MessageBox.Show($"借货单保存成功!\n单号: {billNo}\n合计: {totalAmount:N2}",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (_selectedBill != null)
            {
                var totalAmount = Details.Sum(d => d.Inprice * d.Amount);
                _selectedBill.Supplier = supplierId;
                _selectedBill.Worker = cboWorker.SelectedValue?.ToString() ?? cboWorker.Text.Trim();
                _selectedBill.Total = totalAmount;
                _selectedBill.Memo = txtMemo.Text.Trim();
                await _viewModel.UpdateBillAsync(_selectedBill);
                MessageBox.Show("借货单更新成功!", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            LoadBillList();
            OnCancel();
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
        // 旧"结算"逻辑已废弃（会把 flag 错设为 Confirmed=1），统一走「归还」流程
        await Task.Run(() => { });
        if (_selectedBill == null || string.IsNullOrEmpty(_selectedBill.Sn))
        {
            MessageBox.Show("请选择要归还的单据", "提示");
            return;
        }

        if (_selectedBill.Flag != 3)
        {
            MessageBox.Show("只能归还状态为'在借'的单据", "提示");
            return;
        }

        if (MessageBox.Show($"确认归还借货单 [{_selectedBill.Sn}]?\n将生成还货单并扣减库存。",
            "确认归还", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            IsEnabled = false;
            var returnSn = await _viewModel.SaveReturnAsync(_selectedBill);
            MessageBox.Show($"归还成功!\n还货单号: {returnSn}", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            LoadBillList();
            OnCancel();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"归还失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }
}
