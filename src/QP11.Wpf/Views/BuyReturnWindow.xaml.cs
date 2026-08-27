using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

/// <summary>
/// 采购退货明细项
/// </summary>
public class BuyReturnDetailItem : INotifyPropertyChanged
{
    private bool _isReturn = true;
    public bool IsReturn { get => _isReturn; set { _isReturn = value; OnPropertyChanged(); OnPropertyChanged(nameof(ReturnTotal)); } }

    public long? Partid { get; set; }
    public string? PartNo { get; set; }
    public string? PartName { get; set; }
    public string? Cartype { get; set; }
    public long StockAmount { get; set; }
    public long OrigAmount { get; set; }
    public long ReturnedAmount { get; set; }
    public long RemainAmount { get; set; }

    private long _returnAmount;
    public long ReturnAmount { get => _returnAmount; set { _returnAmount = value; OnPropertyChanged(); OnPropertyChanged(nameof(ReturnTotal)); } }

    public decimal InPrice { get; set; }
    public decimal ReturnTotal => Math.Round(InPrice * ReturnAmount, 2);

    /// <summary>来源采购单号</summary>
    public string? SourceSn { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? ""));
}

/// <summary>
/// 进货单据列表项（用于左侧DataGrid展示）
/// </summary>
public class BuyBillListItem
{
    public string Sn { get; set; } = "";
    public DateTime Datetime { get; set; }
    public decimal Total { get; set; }
    public string FlagText { get; set; } = "";
}

/// <summary>
/// 采购退货控件：按供应商查询进货单据 → 双击展开明细 → 确认退货
/// </summary>
public partial class BuyReturnWindow : UserControl, ITabContent
{
    private readonly IBuyService _buyService;
    private readonly ISupplierRepository _supplierRepo;

    private List<SupplierInfor> _allSuppliers = new();
    private readonly ObservableCollection<BuyReturnDetailItem> _details = new();
    private string? _currentBillSn;

    // ITabContent 接口
    public string TabTitle => "采购退货";
    public bool HasUnsavedChanges => _details.Count > 0;
    public event EventHandler? RequestClose;

    public BuyReturnWindow(IBuyService buyService, ISupplierRepository supplierRepo)
    {
        _buyService = buyService;
        _supplierRepo = supplierRepo;
        InitializeComponent();
        dgDetails.ItemsSource = _details;
        dtReturnDate.SelectedDate = DateTime.Now;
        Loaded += BuyReturnWindow_Loaded;
        _details.CollectionChanged += (_, _) => UpdateSummary();
    }

    private async void BuyReturnWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _allSuppliers = (await _supplierRepo.GetAllAsync()).ToList();
            cboSupplier.SetSuppliers(_allSuppliers);
            cboSupplier.SupplierSelected += CboSupplier_SupplierSelected;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载供应商失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CboSupplier_SupplierSelected(object? sender, EventArgs e)
    {
        BtnQuery_Click(sender!, new RoutedEventArgs());
    }

    /// <summary>
    /// 按供应商查询所有进货单据
    /// </summary>
    private async void BtnQuery_Click(object sender, RoutedEventArgs e)
    {
        var sid = cboSupplier.SelectedSupplierId;
        if (string.IsNullOrEmpty(sid))
        {
            MessageBox.Show("请先选择供应商", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            cboSupplier.Focus();
            return;
        }

        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var sql = @"
                SELECT b.sn, b.datetime, b.total, b.flag,
                       CASE b.flag WHEN 1 THEN '正常' WHEN 2 THEN '已退' ELSE CONVERT(varchar,b.flag) END AS FlagText
                FROM bill_buy b
                WHERE b.supplier = @Sid
                  AND ISNULL(b.flag, 0) <> 3
                ORDER BY b.datetime DESC";
            var bills = await db.QueryAsync<BuyBillListItem>(sql, new { Sid = sid });
            dgBills.ItemsSource = bills.ToList();

            _details.Clear();
            _currentBillSn = null;
            UpdateSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询进货单据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 双击进货单据 → 加载该单的明细到退货明细区
    /// </summary>
    private async void DgBills_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (dgBills.SelectedItem is not BuyBillListItem bill) return;
        await LoadBillDetailsAsync(bill.Sn);
    }

    /// <summary>
    /// DataGrid中退货数量单元格编辑结束：校验不能超过可退数量
    /// </summary>
    private void DgDetails_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Cancel) return;
        if (e.Column.Header as string != "退货数量") return;
        if (e.Row.Item is not BuyReturnDetailItem item) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (item.ReturnAmount > item.RemainAmount)
            {
                MessageBox.Show($"退货数量({item.ReturnAmount})不能超过可退数量({item.RemainAmount})", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                item.ReturnAmount = item.RemainAmount;
            }
            else if (item.ReturnAmount < 0)
            {
                item.ReturnAmount = 0;
            }
            UpdateSummary();
        });
    }

    private void UpdateSummary()
    {
        // 强制提交DataGrid中正在编辑的单元格，确保数值已写入模型
        dgDetails.CommitEdit(DataGridEditingUnit.Row, true);
        var activeItems = _details.Where(d => d.IsReturn && d.ReturnAmount > 0).ToList();
        txtTotalAmount.Text = activeItems.Sum(d => d.ReturnTotal).ToString("N2");
        txtTotalQty.Text = activeItems.Sum(d => d.ReturnAmount).ToString();
    }

    /// <summary>
    /// 清空当前退货明细
    /// </summary>
    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _details.Clear();
        _currentBillSn = null;
        UpdateSummary();
    }

    /// <summary>
    /// 确认采购退货：通过 BuyService 事务化生成退货单并扣减库存
    /// </summary>
    private async void BtnConfirmReturn_Click(object sender, RoutedEventArgs e)
    {
        var returnItems = _details.Where(d => d.IsReturn && d.ReturnAmount > 0).ToList();
        if (returnItems.Count == 0)
        {
            MessageBox.Show("请先双击进货单据加载明细，并填写退货数量", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sid = cboSupplier.SelectedSupplierId;
        if (string.IsNullOrEmpty(sid))
        {
            MessageBox.Show("请选择供应商", "提示");
            return;
        }

        var totalReturn = returnItems.Sum(d => d.ReturnTotal);
        var confirmMsg = $"确定结算采购退货？{Environment.NewLine}{Environment.NewLine}供应商: {cboSupplier.SearchText}{Environment.NewLine}退货合计: {totalReturn:N2:C}{Environment.NewLine}退货明细: {returnItems.Count} 项";
        var result = MessageBox.Show(confirmMsg, "确认退货", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            IsEnabled = false;

            var returnDetails = returnItems.Select(item => new BuyReturnDetail
            {
                PartId = item.Partid,
                PartNo = item.PartNo,
                PartName = item.PartName,
                Cartype = item.Cartype,
                InPrice = item.InPrice,
                ReturnAmount = item.ReturnAmount,
                SourceSn = item.SourceSn
            }).ToList();

            var returnSn = await _buyService.CreateBuyReturnAsync(sid, cboSupplier.SearchText, returnDetails);
            txtReturnSn.Text = returnSn;

            MessageBox.Show($"采购退货成功!{Environment.NewLine}{Environment.NewLine}退货单号: {returnSn}{Environment.NewLine}退货合计: {totalReturn:N2:C}",
                "提示", MessageBoxButton.OK, MessageBoxImage.Information);

            // 记录当前采购单号，用于刷新后重新加载
            var sourceSnToReload = _currentBillSn;

            _details.Clear();
            _currentBillSn = null;
            UpdateSummary();

            // 先刷新左侧单据列表，再重新加载明细（确保可退/已退数量更新）
            await RefreshBillListAsync();
            if (!string.IsNullOrEmpty(sourceSnToReload))
            {
                await LoadBillDetailsAsync(sourceSnToReload);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"退货失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    /// <summary>
    /// 刷新左侧进货单据列表（异步，可await）
    /// </summary>
    private async Task RefreshBillListAsync()
    {
        var sid = cboSupplier.SelectedSupplierId;
        if (string.IsNullOrEmpty(sid)) return;

        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var sql = @"
                SELECT b.sn, b.datetime, b.total, b.flag,
                       CASE b.flag WHEN 1 THEN '正常' WHEN 2 THEN '已退' ELSE CONVERT(varchar,b.flag) END AS FlagText
                FROM bill_buy b
                WHERE b.supplier = @Sid
                  AND ISNULL(b.flag, 0) <> 3
                ORDER BY b.datetime DESC";
            var bills = await db.QueryAsync<BuyBillListItem>(sql, new { Sid = sid });
            dgBills.ItemsSource = bills.ToList();
        }
        catch (Exception ex)
        {
            // 静默失败，不影响主流程
            System.Diagnostics.Debug.WriteLine($"刷新单据列表失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 加载指定采购单的退货明细（含已退数量计算），供退货成功后刷新调用
    /// </summary>
    private async Task LoadBillDetailsAsync(string sn)
    {
        try
        {
            _currentBillSn = sn;
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();

            var detailSql = @"
                SELECT d.partid, d.partno, d.name, d.cartype, d.amount, d.inprice, d.intotal
                FROM detail_buy d
                WHERE d.sn = @Sn AND d.amount > 0";
            var details = await db.QueryAsync<dynamic>(detailSql, new { Sn = sn });

            var partids = details.Select(d => (long?)d.partid).Where(p => p.HasValue).Select(p => p!.Value).Distinct().ToList();
            var stockMap = new Dictionary<long, long>();
            if (partids.Count > 0)
            {
                var stockSql = "SELECT partid, amount FROM part_stock WHERE partid IN @Partids";
                var stocks = await db.QueryAsync<(long partid, long amount)>(stockSql, new { Partids = partids });
                foreach (var s in stocks) stockMap[s.partid] = s.amount;
            }

            var sid = cboSupplier.SelectedSupplierId ?? "";
            var returnedSql = @"
                SELECT d.partid, SUM(ABS(d.amount)) AS returned_amount
                FROM detail_buy d
                INNER JOIN bill_buy b ON b.sn = d.sn
                WHERE b.supplier = @Sid
                  AND d.tsn = @Tsn
                  AND d.amount < 0
                  AND ISNULL(b.flag, 0) <> 3
                GROUP BY d.partid";
            var returnedMap = (await db.QueryAsync<(long partid, long returned_amount)>(returnedSql, new { Sid = sid, Tsn = sn }))
                .ToDictionary(x => x.partid, x => x.returned_amount);

            _details.Clear();
            foreach (var d in details)
            {
                var partid = (long?)d.partid;
                var origAmount = (long)(d.amount ?? 0);
                var alreadyReturned = partid.HasValue && returnedMap.TryGetValue(partid.Value, out var r) ? r : 0L;
                var remain = Math.Max(0, origAmount - alreadyReturned);

                if (remain <= 0) continue;

                _details.Add(new BuyReturnDetailItem
                {
                    Partid = partid,
                    PartNo = (string?)d.partno ?? "",
                    PartName = (string?)d.name ?? "",
                    Cartype = (string?)d.cartype ?? "",
                    StockAmount = partid.HasValue && stockMap.TryGetValue(partid.Value, out var stk) ? stk : 0L,
                    OrigAmount = origAmount,
                    ReturnedAmount = alreadyReturned,
                    RemainAmount = remain,
                    ReturnAmount = 0,
                    InPrice = (decimal)(d.inprice ?? 0),
                    SourceSn = sn
                });
            }

            UpdateSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载明细失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #region ITabContent
    public void OnAdd() { }
    public void OnEdit() { }
    public void OnQuery() => BtnQuery_Click(this, new RoutedEventArgs());
    public void OnDelete()
    {
        if (dgDetails.SelectedItem is BuyReturnDetailItem item)
        {
            _details.Remove(item);
            UpdateSummary();
        }
    }
    public void OnSave() { }
    public void OnSettle() => BtnConfirmReturn_Click(this, new RoutedEventArgs());
    public void OnPrint() { }
    public void OnReturn() { }
    public void OnCancel() => BtnClear_Click(this, new RoutedEventArgs());
    public void OnHistory() { }
    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);
    #endregion
}
