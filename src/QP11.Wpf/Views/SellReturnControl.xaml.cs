using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Dapper;
using QP11.Core.Entities;
using QP11.Services;
using QP11.Wpf.ViewModels;

namespace QP11.Wpf.Views;

public class SellReturnItem : INotifyPropertyChanged
{
    public string? SourceSn { get; set; }
    public long? SourcePartId { get; set; }
    public string? PartNo { get; set; }
    public string? PartName { get; set; }
    public string? Cartype { get; set; }
    public string? Unit { get; set; }
    public string? Place { get; set; }

    private int _amount;
    public int Amount
    {
        get => _amount;
        set { _amount = value; OnPropertyChanged(nameof(Amount)); OnPropertyChanged(nameof(SubTotal)); }
    }

    private decimal _price;
    public decimal Price
    {
        get => _price;
        set { _price = value; OnPropertyChanged(nameof(Price)); OnPropertyChanged(nameof(SubTotal)); }
    }

    public decimal SubTotal => Amount * Price;

    private bool _toWaste;
    public bool ToWaste
    {
        get => _toWaste;
        set { _toWaste = value; OnPropertyChanged(nameof(ToWaste)); }
    }

    public string? Memo { get; set; }

    public int MaxReturnAmount { get; set; }

    /// <summary>关联的进货单号（退回供应商时使用）</summary>
    private string? _sourceBuySn;
    public string? SourceBuySn { get => _sourceBuySn; set { _sourceBuySn = value; OnPropertyChanged(nameof(SourceBuySn)); } }
    /// <summary>关联的供应商名称</summary>
    private string? _sourceBuySupplier;
    public string? SourceBuySupplier { get => _sourceBuySupplier; set { _sourceBuySupplier = value; OnPropertyChanged(nameof(SourceBuySupplier)); } }
    /// <summary>关联的供应商ID</summary>
    private string? _sourceBuySupplierSid;
    public string? SourceBuySupplierSid { get => _sourceBuySupplierSid; set { _sourceBuySupplierSid = value; OnPropertyChanged(nameof(SourceBuySupplierSid)); } }
    /// <summary>原始进价（用于采购退货金额计算）</summary>
    private decimal _sourceBuyInPrice;
    public decimal SourceBuyInPrice { get => _sourceBuyInPrice; set { _sourceBuyInPrice = value; OnPropertyChanged(nameof(SourceBuyInPrice)); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class SellReturnControl : UserControl, ITabContent
{
    private readonly SellReturnViewModel _viewModel;

    private List<ClientInfor> _allClients = new();
    private List<dynamic> _allSourceDetails = new();

    // 查询输入框数组（用于键盘导航）
    private TextBox[] _queryTextBoxes = Array.Empty<TextBox>();

    // 编辑模式
    private bool _isEditMode = false;
    private string? _editSn;

    public ObservableCollection<SellReturnItem> ReturnDetails => _viewModel.ReturnDetails;

    public string TabTitle => "销售退货";
    public bool HasUnsavedChanges => ReturnDetails.Count > 0;
    public event EventHandler? RequestClose;

    private DispatcherTimer? _searchTimer;
    private CancellationTokenSource? _searchCts;
    private Task? _dropdownsLoadTask; // 跟踪下拉框加载任务

    public SellReturnControl(SellReturnViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _queryTextBoxes = new[] { txtPartNo, txtPartName, txtCartype };
        dgReturnDetails.ItemsSource = ReturnDetails;
        dtBillDate.SelectedDate = DateTime.Now;
        Loaded += SellReturnControl_Loaded;
        ReturnDetails.CollectionChanged += (_, _) => UpdateSummary();
    }

    private async void SellReturnControl_Loaded(object sender, RoutedEventArgs e)
    {
        _dropdownsLoadTask = LoadDropdownsAsync();
        await _dropdownsLoadTask;
        InitSearchTimer();
    }

    private async Task LoadDropdownsAsync()
    {
        try
        {
            // 并行加载客户和业务员，避免串行等待
            var clientsTask = _viewModel.LoadClientsAsync();
            var usersTask = _viewModel.LoadUsersAsync();
            await Task.WhenAll(clientsTask, usersTask);

            _allClients = await clientsTask;
            cboClient.SetClients(_allClients);
            cboClient.ClientSelected += CboClient_ClientSelected;

            var users = await usersTask;
            cboWorker.ItemsSource = users;
            cboWorker.DisplayMemberPath = "Name";
            cboWorker.SelectedValuePath = "Username";

            // 自动填充当前登录用户为业务员
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

    private void InitSearchTimer()
    {
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            LoadSourceDetailsAsync();
        };
    }

    private void TxtQuery_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchTimer?.Stop();
        _searchTimer?.Start();
    }

    private void QueryInput_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
            Dispatcher.BeginInvoke(() => tb.SelectAll());
    }

    private void QueryInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var idx = Array.IndexOf(_queryTextBoxes, tb);
        if (idx < 0) return;

        switch (e.Key)
        {
            // 查询框水平排列，用左右方向键切换
            case Key.Right when tb.CaretIndex == tb.Text.Length:
                if (idx < _queryTextBoxes.Length - 1)
                {
                    _queryTextBoxes[idx + 1].Focus();
                    e.Handled = true;
                }
                break;
            case Key.Left when tb.CaretIndex == 0:
                if (idx > 0)
                {
                    _queryTextBoxes[idx - 1].Focus();
                    e.Handled = true;
                }
                break;
            case Key.Enter:
                if (idx < _queryTextBoxes.Length - 1)
                {
                    _queryTextBoxes[idx + 1].Focus();
                }
                else
                {
                    // 最后一个查询框按回车 → 触发查询
                    _searchTimer?.Stop();
                    LoadSourceDetailsAsync();
                }
                e.Handled = true;
                break;
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            var title = btn.Content?.ToString() ?? tag;
            var mainWin = Window.GetWindow(this) as MainWindow;
            mainWin?.OpenFunctionTab(tag, title);
        }
    }

    private void NavCalc_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start("calc.exe"); } catch (Exception ex) { Serilog.Log.Warning(ex, "打开计算器失败"); }
    }

    private void NavNotepad_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start("notepad.exe"); } catch (Exception ex) { Serilog.Log.Warning(ex, "打开记事本失败"); }
    }

    private void CboClient_ClientSelected(object? sender, EventArgs e)
    {
        if (cboClient.SelectedClient != null)
        {
            LoadSourceDetailsAsync();
        }
    }

    private void BtnSelectClient_Click(object sender, RoutedEventArgs e)
    {
        cboClient.Focus();
    }

    private async void LoadSourceDetailsAsync()
    {
        var clientId = cboClient.SelectedClientId?.ToString();
        if (string.IsNullOrEmpty(clientId))
        {
            dgSourceDetails.ItemsSource = null;
            _allSourceDetails.Clear();
            return;
        }

        // 取消前一次未完成的查询
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        try
        {
            var partNo = txtPartNo.Text.Trim();
            var partName = txtPartName.Text.Trim();
            var cartype = txtCartype.Text.Trim();

            var result = await _viewModel.LoadSourceDetailsAsync(clientId,
                string.IsNullOrEmpty(partNo) ? null : partNo,
                string.IsNullOrEmpty(partName) ? null : partName,
                string.IsNullOrEmpty(cartype) ? null : cartype);

            // 若已被新查询取消，丢弃旧结果
            if (cts.IsCancellationRequested) return;

            _allSourceDetails = result;
            dgSourceDetails.ItemsSource = _allSourceDetails;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
                MessageBox.Show($"加载拿货明细失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        LoadSourceDetailsAsync();
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        txtPartNo.Text = "";
        txtPartName.Text = "";
        txtCartype.Text = "";
        LoadSourceDetailsAsync();
    }

    private void DgSourceDetails_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (dgSourceDetails.SelectedItem == null) return;

        var row = dgSourceDetails.SelectedItem as dynamic;
        if (row == null) return;

        try
        {
            var partId = (long?)row.partid;
            var partNo = (string?)row.partno ?? "";
            var partName = (string?)row.name ?? "";
            var cartype = (string?)row.cartype ?? "";
            var origPrice = (decimal?)row.price ?? 0;
            var origAmount = (long?)row.amount ?? 0;
            var remainAmount = (long?)row.remain_amount ?? 0;
            var sourceSn = (string?)row.sn ?? "";
            var place = (string?)row.place ?? "";
            var unit = (string?)row.unit ?? "";

            if (remainAmount <= 0)
            {
                MessageBox.Show("该配件已全部退完，无法再次退货", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existing = ReturnDetails.FirstOrDefault(r => r.SourceSn == sourceSn && r.SourcePartId == partId);
            if (existing != null)
            {
                MessageBox.Show("该配件已在退货明细中，请直接修改退货数量", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SellReturnEditDialog(partId, partNo, partName, cartype, origPrice, (int)remainAmount)
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() == true && dlg.IsConfirmed)
            {
                var item = new SellReturnItem
                {
                    SourceSn = sourceSn,
                    SourcePartId = partId,
                    PartNo = partNo,
                    PartName = partName,
                    Cartype = cartype,
                    Unit = unit,
                    Place = place,
                    Amount = dlg.ReturnAmount,
                    Price = dlg.ReturnPrice,
                    ToWaste = dlg.ToWaste,
                    Memo = "",
                    MaxReturnAmount = (int)remainAmount
                };

                // 勾选了废品仓 → 弹出进货记录选择框，让用户选择退给哪个供应商
                if (dlg.ToWaste)
                {
                    if (!partId.HasValue)
                    {
                        // partId为空时无法关联进货记录，走纯废品仓逻辑
                        MessageBox.Show("该配件无配件ID，将按纯废品仓处理", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        var buyDlg = new SelectBuyRecordDialog(partId.Value, partNo ?? "", partName ?? "", dlg.ReturnAmount)
                        {
                            Owner = Window.GetWindow(this)
                        };
                        buyDlg.ShowDialog();

                        if (buyDlg.IsConfirmed)
                        {
                            // 用户选择了进货记录 → 关联到退货明细，结算时生成采购退货单
                            item.SourceBuySn = buyDlg.SelectedBuySn;
                            item.SourceBuySupplier = buyDlg.SelectedSupplierName;
                            item.SourceBuySupplierSid = buyDlg.SelectedSupplierSid;
                            item.SourceBuyInPrice = buyDlg.SelectedInPrice;
                        }
                        // 未选择 → 保持ToWaste=true但无进货关联，走原来的废品仓逻辑
                    }
                }

                ReturnDetails.Add(item);
                UpdateSummary();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"添加退货明细失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DgReturnDetails_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && dgReturnDetails.SelectedItem is SellReturnItem item)
        {
            ReturnDetails.Remove(item);
            UpdateSummary();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 退货明细行双击：重新选择退货方式（数量/价格/废品仓/进货记录）
    /// </summary>
    private void DgReturnDetails_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (dgReturnDetails.SelectedItem is not SellReturnItem item) return;
        if (!item.SourcePartId.HasValue) return;

        // 先弹出退货编辑对话框（修改数量/价格/废品仓勾选）
        var dlg = new SellReturnEditDialog(item.SourcePartId.Value, item.PartNo ?? "", item.PartName ?? "", item.Cartype ?? "", item.Price, item.MaxReturnAmount + item.Amount)
        {
            Owner = Window.GetWindow(this)
        };
        // 预填当前值
        dlg.SetCurrentValues(item.Amount, item.Price, item.ToWaste);

        dlg.ShowDialog();

        if (dlg.IsConfirmed)
        {
            item.Amount = dlg.ReturnAmount;
            item.Price = dlg.ReturnPrice;
            item.ToWaste = dlg.ToWaste;
            UpdateSummary();

            // 勾选了废品仓 → 弹出进货记录选择框
            if (dlg.ToWaste && item.SourcePartId.HasValue)
            {
                var buyDlg = new SelectBuyRecordDialog(item.SourcePartId.Value, item.PartNo ?? "", item.PartName ?? "", dlg.ReturnAmount)
                {
                    Owner = Window.GetWindow(this)
                };
                buyDlg.ShowDialog();

                if (buyDlg.IsConfirmed)
                {
                    item.SourceBuySn = buyDlg.SelectedBuySn;
                    item.SourceBuySupplier = buyDlg.SelectedSupplierName;
                    item.SourceBuySupplierSid = buyDlg.SelectedSupplierSid;
                    item.SourceBuyInPrice = buyDlg.SelectedInPrice;
                }
                else
                {
                    // 取消 → 清除进货关联，改为纯废品仓
                    item.SourceBuySn = null;
                    item.SourceBuySupplier = null;
                    item.SourceBuySupplierSid = null;
                    item.SourceBuyInPrice = 0;
                }
            }
            else
            {
                // 取消了废品仓 → 清除进货关联
                item.SourceBuySn = null;
                item.SourceBuySupplier = null;
                item.SourceBuySupplierSid = null;
                item.SourceBuyInPrice = 0;
            }
        }
    }

    private void UpdateSummary()
    {
        txtSumTotal.Text = ReturnDetails.Sum(d => d.SubTotal).ToString("N2");
        txtSumAmount.Text = ReturnDetails.Sum(d => d.Amount).ToString();
    }

    private async void BtnSettle_Click(object sender, RoutedEventArgs e)
    {
        await SettleReturn();
    }

    private async Task SettleReturn()
    {
        if (ReturnDetails.Count == 0)
        {
            MessageBox.Show("请添加退货明细", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var clientId = cboClient.SelectedClientId?.ToString();
        if (string.IsNullOrEmpty(clientId))
        {
            MessageBox.Show("请选择客户", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            cboClient.Focus();
            return;
        }

        foreach (var item in ReturnDetails)
        {
            if (item.Amount <= 0)
            {
                MessageBox.Show($"配件 [{item.PartNo} {item.PartName}] 退货数量必须大于0", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (item.Amount > item.MaxReturnAmount)
            {
                MessageBox.Show($"配件 [{item.PartNo} {item.PartName}] 退货数量({item.Amount})超过可退数量({item.MaxReturnAmount})", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (item.Price < 0)
            {
                MessageBox.Show($"配件 [{item.PartNo} {item.PartName}] 退货单价不能为负数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var totalReturn = ReturnDetails.Sum(d => d.SubTotal);
        var confirmMsg = _isEditMode
            ? $"确定保存修改？\n退货合计: ¥{totalReturn:N2}\n退货明细: {ReturnDetails.Count} 项"
            : $"确定结算退货单？\n退货合计: ¥{totalReturn:N2}\n退货明细: {ReturnDetails.Count} 项";
        var confirm = MessageBox.Show(confirmMsg, _isEditMode ? "确认修改" : "确认退货", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsEnabled = false;

            var workerId = cboWorker.SelectedValue?.ToString() ?? cboWorker.Text.Trim();
            if (string.IsNullOrEmpty(workerId) && App.CurrentUser != null)
            {
                workerId = await _viewModel.ResolveWorkerIdFromNameAsync(App.CurrentUser.Name!) ?? "";
            }

            var bill = new BillSell
            {
                Client = clientId,
                Worker = workerId,
                Operator = App.CurrentUser?.Username,
                Datetime = (dtBillDate.SelectedDate?.Date ?? DateTime.Now.Date) + DateTime.Now.TimeOfDay,
                Total = -totalReturn,
                BillTotal = -totalReturn,
                DiscountRate = 0,
                TotalPayment = -totalReturn,
                BillPayment = -totalReturn,
                Collection = 0,
                Cash = 0,
                Weixin = 0,
                Zhifubao = 0,
                Checks = 0,
                Arrear = -totalReturn,
                Yunfei = 0,
                Flag = 2,
                Memo = txtMemo.Text.Trim()
            };

            await _viewModel.SettleReturnAsync(_editSn, _isEditMode, bill, ReturnDetails, workerId);
            txtBillNo.Text = bill.Sn;

            var successMsg = _isEditMode
                ? $"退货单修改成功!\n退货单号: {bill.Sn}\n退货合计: ¥{totalReturn:N2}"
                : $"退货结算成功!\n退货单号: {bill.Sn}\n退货合计: ¥{totalReturn:N2}";
            MessageBox.Show(successMsg, "提示", MessageBoxButton.OK, MessageBoxImage.Information);

            ClearAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"退货结算失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        ClearAll();
    }

    private void ClearAll()
    {
        ReturnDetails.Clear();
        txtBillNo.Text = "";
        txtMemo.Text = "";
        cboClient.ClearSelection();
        cboClient.IsEnabled = true;
        dgSourceDetails.ItemsSource = null;
        _allSourceDetails.Clear();
        _isEditMode = false;
        _editSn = null;
        UpdateSummary();
    }

    public async void LoadBillForEdit(string sn)
    {
        _isEditMode = true;
        _editSn = sn;

        try
        {
            // 等待下拉框加载完成，避免竞态条件
            if (_dropdownsLoadTask != null)
                await _dropdownsLoadTask;

            var bill = await _viewModel.LoadBillForEditAsync(sn);
            if (bill == null)
            {
                MessageBox.Show($"退货单 {sn} 不存在", "错误");
                _isEditMode = false;
                _editSn = null;
                return;
            }

            txtBillNo.Text = sn;

            // 设置客户（锁定）
            var client = _allClients.FirstOrDefault(c => c.Cid == bill.Client);
            if (client != null)
            {
                cboClient.SetClient(client);
            }
            cboClient.IsEnabled = false;

            // 设置经手人
            if (!string.IsNullOrEmpty(bill.Worker))
            {
                var workerName = await _viewModel.GetWorkerNameAsync(bill.Worker);
                cboWorker.Text = workerName ?? bill.Worker ?? "";
            }

            txtMemo.Text = bill.Memo ?? "";

            // 加载退货明细
            var details = await _viewModel.LoadDetailsAsync(sn);
            ReturnDetails.Clear();
            foreach (var d in details)
            {
                var absAmount = (int)Math.Abs(d.Amount ?? 0);
                var isWaste = !string.IsNullOrEmpty(d.Place) && d.Place.Trim() == "废品仓";
                var memo = d.Memo ?? "";

                // 解析进货关联信息（格式: [BUY:供应商SID|供应商名|进价|进货单号]原备注）
                string? sourceBuySn = null;
                string? sourceBuySupplier = null;
                string? sourceBuySupplierSid = null;
                decimal sourceBuyInPrice = 0;
                var cleanMemo = memo;

                if (memo.StartsWith("[BUY:") && memo.IndexOf(']') > 0)
                {
                    try
                    {
                        var endIdx = memo.IndexOf(']');
                        var buyInfo = memo[5..endIdx].Split('|');
                        if (buyInfo.Length >= 4)
                        {
                            sourceBuySupplierSid = buyInfo[0];
                            sourceBuySupplier = buyInfo[1];
                            decimal.TryParse(buyInfo[2], out sourceBuyInPrice);
                            sourceBuySn = buyInfo[3];
                        }
                        cleanMemo = memo[(endIdx + 1)..];
                    }
                    catch (Exception ex) { Serilog.Log.Warning(ex, "解析退货备注中的采购信息失败"); }
                }

                ReturnDetails.Add(new SellReturnItem
                {
                    SourceSn = d.Tsn,
                    SourcePartId = d.Partid,
                    PartNo = d.Partno,
                    PartName = d.Name,
                    Cartype = d.Cartype,
                    Unit = d.Unit,
                    Place = d.Place,
                    Price = Math.Abs(d.Price ?? 0),
                    Amount = absAmount,
                    ToWaste = isWaste,
                    Memo = cleanMemo,
                    MaxReturnAmount = absAmount,
                    SourceBuySn = sourceBuySn,
                    SourceBuySupplier = sourceBuySupplier,
                    SourceBuySupplierSid = sourceBuySupplierSid,
                    SourceBuyInPrice = sourceBuyInPrice
                });
            }
            UpdateSummary();

            // 加载源单据明细（用于追加退货项）
            LoadSourceDetailsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载退货单失败: {ex.Message}", "错误");
            _isEditMode = false;
            _editSn = null;
        }
    }

    #region ITabContent
    public void OnAdd() { }
    public void OnEdit() { }
    public void OnQuery() => LoadSourceDetailsAsync();
    public void OnDelete()
    {
        if (dgReturnDetails.SelectedItem is SellReturnItem item)
        {
            ReturnDetails.Remove(item);
            UpdateSummary();
        }
    }
    public void OnSave() { }
    public void OnSettle() => BtnSettle_Click(this, new RoutedEventArgs());
    public async void OnPrint()
    {
        if (ReturnDetails.Count == 0) { MessageBox.Show("没有可打印的退货数据", "提示"); return; }
        try
        {
            var clientId = cboClient.SelectedClientId?.ToString();
            var billData = new BillPrintData
            {
                BillType = "退货",
                Sn = txtBillNo.Text,
                DateText = DateTime.Now.ToString("yyyy-MM-dd"),
                PartnerName = cboClient.SearchText,
                PartnerPhone = _allClients.FirstOrDefault(c => c.Cid == clientId)?.Mobile
                    ?? _allClients.FirstOrDefault(c => c.Cid == clientId)?.Tel ?? "",
                PartnerContact = _allClients.FirstOrDefault(c => c.Cid == clientId)?.Linkman ?? "",
                PartnerAddress = _allClients.FirstOrDefault(c => c.Cid == clientId)?.Address ?? "",
                TotalAmount = -ReturnDetails.Sum(d => d.SubTotal),
                DeliveryMethod = "自提"
            };
            await billData.LoadCompanyInfoAsync();
            var idx = 1;
            foreach (var d in ReturnDetails)
            {
                billData.Items.Add(new BillPrintItem
                {
                    Index = idx++,
                    PartNo = d.PartNo,
                    PartName = d.PartName,
                    Cartype = d.Cartype,
                    Unit = d.Unit,
                    Price = d.Price,
                    PfPrice = 0,
                    BillPrice = d.Price,
                    Amount = d.Amount,
                    Subtotal = d.SubTotal,
                    Place = d.Place,
                    Area = "",
                    Brand = "",
                    DiscountRate = 0,
                    Memo = d.Memo
                });
            }
            var dlg = new PrintPreviewWindow(billData, $"退货单-{txtBillNo.Text}")
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "打印预览退货单失败"); MessageBox.Show($"打印预览失败: {ex.Message}", "错误"); }
    }
    public void OnReturn() { }
    public void OnCancel() => ClearAll();
    public void OnHistory() { }
    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);
    #endregion
}
