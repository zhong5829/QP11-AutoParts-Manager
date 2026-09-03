using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Core.Models;
using QP11.Services;
using QP11.Wpf.Helpers;
using QP11.Wpf.ViewModels;

namespace QP11.Wpf.Views;

public class BuyDetailItem : INotifyPropertyChanged
{
    public long PartId { get; set; }
    public string? PartNo { get; set; }
    public string? PartName { get; set; }
    public string? CarName { get; set; }
    public string? Cartype { get; set; }
    public string? Unit { get; set; }
    public string? Place { get; set; }
    public string? Memo { get; set; }

    private decimal _inPrice;
    public decimal InPrice
    {
        get => _inPrice;
        set { _inPrice = value; OnPropertyChanged(nameof(InPrice)); OnPropertyChanged(nameof(SubTotal)); }
    }

    private decimal _lsPrice;
    public decimal LsPrice
    {
        get => _lsPrice;
        set { _lsPrice = value; OnPropertyChanged(nameof(LsPrice)); }
    }

    private decimal _pfPrice;
    public decimal PfPrice
    {
        get => _pfPrice;
        set { _pfPrice = value; OnPropertyChanged(nameof(PfPrice)); }
    }

    private decimal _amount = 1;
    public decimal Amount
    {
        get => _amount;
        set { _amount = value; OnPropertyChanged(nameof(Amount)); OnPropertyChanged(nameof(SubTotal)); }
    }

    private decimal _originalAmount;
    public decimal OriginalAmount
    {
        get => _originalAmount;
        set { _originalAmount = value; OnPropertyChanged(nameof(OriginalAmount)); }
    }

    public decimal SubTotal => Math.Round(InPrice * Amount, 2);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class BuyBillDisplay
{
    public string? Sn { get; set; }
    public DateTime? Datetime { get; set; }
    public string? SupplierName { get; set; }
    public string? WorkerName { get; set; }
    public decimal? Total { get; set; }
    public string? FlagText { get; set; }
    public int Flag { get; set; }
}

public partial class BuyControl : UserControl, ITabContent
{
    private readonly BuyViewModel _viewModel;

    private BillBuy? _currentBill;
    private int _currentFlag;
    private List<NameDiffUpdate> _pendingNameUpdates = new();
    private List<SupplierInfor> _allSuppliers = new();
    private Dictionary<string, string> _supplierPyCache = new();

    public ObservableCollection<BuyDetailItem> Details => _viewModel.Details;
    public string TabTitle => "采购开单";
    public bool HasUnsavedChanges => Details.Count > 0;
    public event EventHandler? RequestClose;

    public BuyControl(BuyViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        dgDetails.ItemsSource = Details;
        dtBillDate.SelectedDate = DateTime.Now;
        _currentFlag = 0;
        LoadDropdowns();
        LoadBillList();
    }

    private async void LoadDropdowns()
    {
        try
        {
            _allSuppliers = await _viewModel.LoadSuppliersAsync();
            _supplierPyCache = _allSuppliers.Where(s => !string.IsNullOrEmpty(s.Name))
                .ToDictionary(s => s.Sid ?? "", s => PinyinHelper.GetPinyinInitials(s.Name!));
            cboSupplier.SetSuppliers(_allSuppliers);

            var users = await _viewModel.LoadUsersAsync();
            cboWorker.ItemsSource = users;
            cboWorker.DisplayMemberPath = "Name";
            cboWorker.SelectedValuePath = "Username";

            // 默认选中当前登录用户
            var currentUsername = App.CurrentUser?.Username;
            if (!string.IsNullOrEmpty(currentUsername))
            {
                var currentUser = users.FirstOrDefault(u => u.Username == currentUsername);
                if (currentUser != null)
                    cboWorker.SelectedItem = currentUser;
            }
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
            var display = await _viewModel.LoadBillListAsync(_currentFlag);
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
        if (rbUnsettled.IsChecked == true) _currentFlag = 0;
        else if (rbSettled.IsChecked == true) _currentFlag = 1;
        else if (rbReturn.IsChecked == true) _currentFlag = 2;

        // 保留当前采购员，切换状态时不重置
        var savedWorker = cboWorker.Text;
        ClearBill();
        cboWorker.Text = savedWorker;
        LoadBillList();
    }

    private async void DgBills_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgBills.SelectedItem is not BuyBillDisplay row) return;
        if (string.IsNullOrEmpty(row.Sn)) return;

        try
        {
            _currentBill = await _viewModel.LoadBillAsync(row.Sn);
            if (_currentBill == null) return;

            txtBillNo.Text = _currentBill.Sn;
            dtBillDate.SelectedDate = _currentBill.Datetime;

            var supplierName = await _viewModel.GetSupplierNameAsync(_currentBill.Supplier ?? "");
            var supplier = _allSuppliers.FirstOrDefault(s => s.Sid == _currentBill.Supplier);
            if (supplier != null)
                cboSupplier.SetSupplier(supplier);
            else
                cboSupplier.SearchText = supplierName ?? _currentBill.Supplier ?? "";

            var workerName = await _viewModel.GetWorkerNameAsync(_currentBill.Worker ?? "");
            cboWorker.Text = workerName ?? _currentBill.Worker ?? "";
            txtInvoice.Text = _currentBill.Invoice ?? "";
            txtTotal.Text = _currentBill.Total?.ToString("N2") ?? "0";
            txtZhifubao.Text = _currentBill.Zhifubao?.ToString() ?? "0";
            txtWeixin.Text = _currentBill.Weixin?.ToString() ?? "0";
            txtYunfei.Text = _currentBill.Yunfei?.ToString() ?? "0";
            txtArrear.Text = _currentBill.Arrear?.ToString() ?? "0";
            txtMemo.Text = _currentBill.Memo ?? "";

            Details.Clear();
            var details = await _viewModel.LoadDetailsAsync(row.Sn);
            foreach (var d in details)
            {
                Details.Add(new BuyDetailItem
                {
                    PartId = d.Partid ?? 0,
                    PartNo = d.Partno,
                    PartName = d.Name,
                    CarName = d.Carname,
                    Cartype = d.Cartype,
                    InPrice = d.Inprice == null ? 0m : Convert.ToDecimal(d.Inprice),
                    LsPrice = d.Lsprice == null ? 0m : Convert.ToDecimal(d.Lsprice),
                    PfPrice = d.Pfprice == null ? 0m : Convert.ToDecimal(d.Pfprice),
                    Amount = d.Amount ?? 0,
                    OriginalAmount = d.Amount ?? 0,
                    Unit = d.Unit,
                    Place = d.Place,
                    Memo = d.Memo
                });
            }
            UpdateTotal();
            SetHeaderReadOnly(row.Flag == (int)BusinessConstants.BillFlag.Confirmed || row.Flag == (int)BusinessConstants.BillFlag.Returned);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载单据失败: {ex.Message}", "错误");
        }
    }

    private void UpdateTotal()
    {
        var total = Details.Sum(d => d.SubTotal);
        txtTotal.Text = total.ToString("N2");
        UpdateArrear();
    }

    private void UpdateArrear()
    {
        if (!IsLoaded) return;

        var total = decimal.TryParse(txtTotal.Text, out var t) ? t : 0;
        var zhifubao = decimal.TryParse(txtZhifubao.Text, out var z) ? z : 0;
        var weixin = decimal.TryParse(txtWeixin.Text, out var w) ? w : 0;
        var yunfei = decimal.TryParse(txtYunfei.Text, out var yf) ? yf : 0;
        var paid = zhifubao + weixin;
        txtArrear.Text = Math.Max(0, total + yunfei - paid).ToString("N2");
    }

    private void Payment_TextChanged(object sender, TextChangedEventArgs e) => UpdateArrear();

    private void BtnStyleAdd_Click(object sender, RoutedEventArgs e)
    {
        OnAdd();
    }

    private void BtnExcelImport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ExcelImportDialog { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true || !dlg.ImportConfirmed) return;

        if (dlg.UpdatePartNames && dlg.NameDiffItems.Count > 0)
        {
            _pendingNameUpdates = dlg.NameDiffItems;
        }

        foreach (var item in dlg.ImportedDetails)
        {
            if (item.PartId > 0)
            {
                var existing = Details.FirstOrDefault(d => d.PartId == item.PartId);
                if (existing != null)
                {
                    existing.Amount += item.Amount;
                }
                else
                {
                    Details.Add(item);
                }
            }
            else
            {
                var existing = Details.FirstOrDefault(d =>
                    string.Equals(d.PartNo?.Trim(), item.PartNo?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Amount += item.Amount;
                }
                else
                {
                    Details.Add(item);
                }
            }
        }
        UpdateTotal();
    }

    private void BtnBuyToSell_Click(object sender, RoutedEventArgs e)
    {
        if (Details.Count == 0)
        {
            MessageBox.Show("当前无采购明细可转", "提示");
            return;
        }
        MessageBox.Show("采购转销售功能开发中", "提示");
    }

    private async void BtnPrintBill_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBill == null)
        {
            MessageBox.Show("请先选择要打印的单据", "提示");
            return;
        }
        try
        {
            var billData = new BillPrintData
            {
                BillType = "采购",
                Sn = _currentBill.Sn,
                DateText = _currentBill.Datetime?.ToString("yyyy-MM-dd") ?? "",
                PartnerName = cboSupplier.SearchText,
                PartnerPhone = _allSuppliers.FirstOrDefault(s => s.Sid == _currentBill.Supplier)?.Mobile
                    ?? _allSuppliers.FirstOrDefault(s => s.Sid == _currentBill.Supplier)?.Tel ?? "",
                PartnerContact = _allSuppliers.FirstOrDefault(s => s.Sid == _currentBill.Supplier)?.Linkman ?? "",
                PartnerAddress = _allSuppliers.FirstOrDefault(s => s.Sid == _currentBill.Supplier)?.Address ?? "",
                WorkerName = _currentBill.Worker,
                TotalAmount = _currentBill.Total ?? 0,
                Cash = _currentBill.Cash ?? 0,
                Weixin = _currentBill.Weixin ?? 0,
                Zhifubao = _currentBill.Zhifubao ?? 0,
                Arrearage = _currentBill.Arrear ?? 0,
                Memo = _currentBill.Memo ?? "",
                DeliveryMethod = "自提"
            };
            await billData.LoadCompanyInfoAsync();

            var idx = 1;
            foreach (var d in Details)
            {
                billData.Items.Add(new BillPrintItem
                {
                    Index = idx++,
                    PartNo = d.PartNo,
                    PartName = d.PartName,
                    Cartype = d.Cartype ?? "",
                    Unit = d.Unit ?? "",
                    Price = d.InPrice,
                    PfPrice = d.PfPrice,
                    BillPrice = 0,
                    Amount = (int)d.Amount,
                    Subtotal = d.SubTotal,
                    Place = d.Place,
                    Area = "",
                    Brand = "",
                    DiscountRate = 0,
                    Memo = d.Memo
                });
            }

            var dlg = new PrintPreviewWindow(billData, $"采购单-{_currentBill.Sn}")
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打印预览失败: {ex.Message}", "错误");
        }
    }

    #region ITabContent

    public void OnAdd()
    {
        if (string.IsNullOrWhiteSpace(cboSupplier.SearchText))
        {
            MessageBox.Show("请先选择供应商", "提示");
            cboSupplier.Focus();
            return;
        }

        var dlg = new BuyEditDialog(partId: 0, partNo: "");
        dlg.Owner = Window.GetWindow(this);
        dlg.PartConfirmed += OnPartConfirmed;
        dlg.ShowDialog();
        dlg.PartConfirmed -= OnPartConfirmed;
    }

    private void OnPartConfirmed(BuyEditDialog dlg)
    {
        var existing = Details.FirstOrDefault(d => d.PartId == dlg.ResultPartId);
        if (existing != null)
        {
            existing.Amount += dlg.ResultAmount;
        }
        else
        {
            Details.Add(new BuyDetailItem
            {
                PartId = dlg.ResultPartId,
                PartNo = dlg.ResultPartNo,
                PartName = dlg.ResultName,
                CarName = dlg.ResultCarName,
                Cartype = dlg.ResultCarType,
                Unit = dlg.ResultUnit,
                InPrice = dlg.ResultInPrice,
                Amount = dlg.ResultAmount,
                LsPrice = dlg.ResultLsPrice,
                PfPrice = dlg.ResultPfPrice,
                Place = dlg.ResultPlace,
                Memo = dlg.ResultMemo
            });
        }
        UpdateTotal();
    }

    public void OnEdit()
    {
        if (dgDetails.SelectedItem is not BuyDetailItem item) return;

        var dlg = new BuyEditDialog(item.PartId, item.PartNo);
        dlg.SetEditValues(item.Amount, item.InPrice, item.Place, item.Memo);
        dlg.Owner = Window.GetWindow(this);
        if (dlg.ShowDialog() == true && dlg.IsConfirmed)
        {
            item.PartId = dlg.ResultPartId;
            item.PartNo = dlg.ResultPartNo;
            item.PartName = dlg.ResultName;
            item.CarName = dlg.ResultCarName;
            item.Cartype = dlg.ResultCarType;
            item.Unit = dlg.ResultUnit;
            item.InPrice = dlg.ResultInPrice;
            item.Amount = dlg.ResultAmount;
            item.LsPrice = dlg.ResultLsPrice;
            item.PfPrice = dlg.ResultPfPrice;
            item.Place = dlg.ResultPlace;
            item.Memo = dlg.ResultMemo;
            UpdateTotal();
        }
    }

    public void OnQuery()
    {
        // 采购查询已移除，按 F3 或点击查询按钮时跳转到采购明细
        var mainWin = Window.GetWindow(this) as MainWindow;
        mainWin?.OpenFunctionTab("31", "采购明细");
    }

    public void OnDelete()
    {
        if (dgDetails.SelectedItem is BuyDetailItem item)
        {
            Details.Remove(item);
            UpdateTotal();
        }
    }

    public void OnSave() => SaveBill();

    public void OnSettle() => SettleBill();

    public void OnPrint() => BtnPrintBill_Click(this, new RoutedEventArgs());

    public void OnReturn()
    {
        // 采购退货由独立的采购退货功能处理
    }

    private void SetHeaderReadOnly(bool readOnly)
    {
        cboSupplier.IsEnabled = !readOnly;
        dtBillDate.IsEnabled = !readOnly;
        cboWorker.IsReadOnly = readOnly;
        txtInvoice.IsReadOnly = readOnly;
        txtZhifubao.IsReadOnly = readOnly;
        txtWeixin.IsReadOnly = readOnly;
        txtYunfei.IsReadOnly = readOnly;
        txtMemo.IsReadOnly = readOnly;

        var bg = readOnly ? SystemColors.ControlBrush : SystemColors.WindowBrush;
        cboWorker.Background = bg;
        txtInvoice.Background = bg;
        txtZhifubao.Background = bg;
        txtWeixin.Background = bg;
        txtYunfei.Background = bg;
        txtMemo.Background = bg;
    }

    private void ClearBillHeader()
    {
        _currentBill = null;
        txtBillNo.Text = "";
        dtBillDate.SelectedDate = DateTime.Now;
        cboSupplier.ClearSelection();
        // 注意：采购员(cboWorker)不在 ClearBillHeader 中重置，避免结算完成后丢失选择
        txtInvoice.Text = "";
        txtTotal.Text = "";
        txtZhifubao.Text = "0";
        txtWeixin.Text = "0";
        txtYunfei.Text = "0";
        txtArrear.Text = "0";
        txtMemo.Text = "";
        SetHeaderReadOnly(false);
    }

    public void OnCancel() => ClearBill();

    public void OnHistory() { }

    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);

    #endregion

    /// <summary>
    /// 明细网格编辑结束时校验：数量必须大于 0，否则取消提交并提示
    /// </summary>
    private void DgDetails_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;

        if (e.Column is DataGridTextColumn { Binding: Binding { Path.Path: nameof(BuyDetailItem.Amount) } }
            && e.EditingElement is TextBox textBox
            && (!decimal.TryParse(textBox.Text, out var amount) || amount <= 0))
        {
            e.Cancel = true;
            MessageBox.Show("采购数量必须大于 0", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            // 取消提交后回到可编辑状态，让用户直接重新输入
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => dgDetails.BeginEdit()));
        }
    }

    /// <summary>
    /// 检查明细中是否存在数量 ≤ 0 的行（采购入库数量必须为正数）
    /// </summary>
    private bool TryGetInvalidAmountDetail(out BuyDetailItem invalidDetail)
    {
        invalidDetail = Details.FirstOrDefault(d => d.Amount <= 0)!;
        return invalidDetail != null;
    }

    private async void SaveBill()
    {
        if (_currentBill?.Flag == (int)BusinessConstants.BillFlag.Confirmed)
        {
            MessageBox.Show("此单据已结算，不能保存", "提示");
            return;
        }

        if (_currentBill?.Flag == (int)BusinessConstants.BillFlag.Returned)
        {
            MessageBox.Show("此单据为退货单，不能保存", "提示");
            return;
        }

        if (Details.Count == 0)
        {
            MessageBox.Show("请添加采购明细", "提示");
            return;
        }

        if (TryGetInvalidAmountDetail(out var invalidDetail))
        {
            MessageBox.Show($"明细数量必须大于 0，当前行数量无效：{invalidDetail.PartNo} {invalidDetail.PartName}", "提示");
            return;
        }

        if (string.IsNullOrWhiteSpace(cboSupplier.SearchText))
        {
            MessageBox.Show("请选择供应商", "提示");
            cboSupplier.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(cboWorker.Text.Trim()))
        {
            MessageBox.Show("请选择采购员", "提示");
            cboWorker.Focus();
            return;
        }

        try
        {
            IsEnabled = false;

            var billNo = txtBillNo.Text.Trim();

            if (string.IsNullOrEmpty(billNo))
            {
                billNo = await _viewModel.GenerateBuySNAsync();
                txtBillNo.Text = billNo;
            }

            var totalAmount = Details.Sum(d => d.SubTotal);
            var zhifubao = decimal.TryParse(txtZhifubao.Text, out var z) ? z : 0;
            var weixin = decimal.TryParse(txtWeixin.Text, out var w) ? w : 0;
            var yunfei = decimal.TryParse(txtYunfei.Text, out var yf) ? yf : 0;
            var arrear = Math.Max(0, totalAmount + yunfei - zhifubao - weixin);

            var workerName = (cboWorker.SelectedItem as UserInfor)?.Name ?? cboWorker.Text.Trim();
            string workerId = workerName;
            if (!string.IsNullOrEmpty(workerName))
            {
                workerId = await _viewModel.ResolveWorkerIdAsync(workerName);
            }

            var bill = new BillBuy
            {
                Sn = billNo,
                Supplier = cboSupplier.SelectedSupplierId ?? cboSupplier.SearchText.Trim(),
                Worker = workerId,
                Operator = App.CurrentUser?.Username,
                Datetime = (dtBillDate.SelectedDate?.Date ?? DateTime.Now.Date) + DateTime.Now.TimeOfDay,
                Total = totalAmount,
                Invoice = txtInvoice.Text.Trim(),
                Zhifubao = zhifubao,
                Weixin = weixin,
                Yunfei = yunfei,
                Arrear = arrear,
                Flag = 0,
                Memo = txtMemo.Text.Trim()
            };

            var existingBillNo = (_currentBill != null && !string.IsNullOrEmpty(_currentBill.Sn)) ? _currentBill.Sn : null;
            await _viewModel.SaveBillAsync(existingBillNo, bill, Details.ToList());

            // 保存成功后更新 _currentBill，防止再次保存时误判为新单据导致主键冲突
            _currentBill = bill;

            MessageBox.Show($"保存成功!\n单号: {billNo}\n合计: {totalAmount:N2}",
                "提示", MessageBoxButton.OK, MessageBoxImage.Information);

            LoadBillList();
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
        if (Details.Count == 0) return;

        if (TryGetInvalidAmountDetail(out var invalidDetail))
        {
            MessageBox.Show($"明细数量必须大于 0，当前行数量无效：{invalidDetail.PartNo} {invalidDetail.PartName}", "提示");
            return;
        }

        if (string.IsNullOrEmpty(txtBillNo.Text))
        {
            MessageBox.Show("请先保存单据", "提示");
            return;
        }

        if (_currentBill != null && _currentBill.Flag == (int)BusinessConstants.BillFlag.Confirmed)
        {
            MessageBox.Show("该单据已结算", "提示");
            return;
        }

        if (_currentBill != null && _currentBill.Flag == (int)BusinessConstants.BillFlag.Returned)
        {
            MessageBox.Show("退货单不能结算", "提示");
            return;
        }

        var newParts = Details.Where(d => d.PartId == 0 && !string.IsNullOrWhiteSpace(d.PartNo)).ToList();
        if (newParts.Count > 0)
        {
            var msg = $"有 {newParts.Count} 个新配件不在配件库中，结算时将自动新增到配件库。\n\n确认结算?";
            if (MessageBox.Show(msg, "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
        }
        else
        {
            if (MessageBox.Show("确认结算? 结算后将增加库存数量", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;
        }

        try
        {
            IsEnabled = false;

            var sn = txtBillNo.Text.Trim();
            await _viewModel.SettleBillAsync(sn, Details.ToList(), newParts, _pendingNameUpdates);
            _pendingNameUpdates.Clear();

            MessageBox.Show("结算成功，库存已更新", "提示");
            ClearBill();
            LoadBillList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"结算失败: {ex.Message}", "错误");
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void ClearBill()
    {
        Details.Clear();
        _pendingNameUpdates.Clear();
        ClearBillHeader();
    }
}
