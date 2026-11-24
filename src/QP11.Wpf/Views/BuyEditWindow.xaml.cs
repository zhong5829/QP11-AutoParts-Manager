using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class BuyEditWindow : Window
{
    private readonly IBuyRepository _buyRepo = App.ServiceProvider.GetRequiredService<IBuyRepository>();
    private readonly ISupplierRepository _supplierRepo = App.ServiceProvider.GetRequiredService<ISupplierRepository>();
    public ObservableCollection<DetailBuy> Details { get; } = new();
    private BillBuy? _currentBill;

    /// <summary>单据头备注保存成功后通知列表页刷新对应行（sn, memo）</summary>
    public event Action<string, string?>? BillMemoSaved;

    public BuyEditWindow()
    {
        InitializeComponent();
        dgDetails.ItemsSource = Details;
    }

    public BuyEditWindow(string sn) : this()
    {
        _pendingSn = sn;
    }

    private string? _pendingSn;

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (!string.IsNullOrEmpty(_pendingSn))
        {
            txtSn.Text = _pendingSn;
            _pendingSn = null;
            await LoadBillAsync();
        }
    }

    private async void BtnLoad_Click(object sender, RoutedEventArgs e) => await LoadBillAsync();

    private async System.Threading.Tasks.Task LoadBillAsync()
    {
        var sn = txtSn.Text.Trim();
        if (string.IsNullOrEmpty(sn)) { MessageBox.Show("请输入单号", "提示"); return; }
        try
        {
            _currentBill = await _buyRepo.GetBySnAsync(sn);
            if (_currentBill == null) { MessageBox.Show("未找到该采购单", "提示"); return; }

            txtBillNo.Text = _currentBill.Sn;
            txtBillDate.Text = _currentBill.Datetime?.ToString("yyyy-MM-dd") ?? "";
            txtInvoice.Text = _currentBill.Invoice ?? "";
            txtTotal.Text = _currentBill.Total?.ToString("N2") ?? "0.00";
            txtZhifubao.Text = _currentBill.Zhifubao?.ToString("N2") ?? "0.00";
            txtWeixin.Text = _currentBill.Weixin?.ToString("N2") ?? "0.00";
            txtYunfei.Text = _currentBill.Yunfei?.ToString("N2") ?? "0.00";
            txtArrear.Text = _currentBill.Arrear?.ToString("N2") ?? "0.00";
            txtMemo.Text = _currentBill.Memo ?? "";

            // 供应商名称
            if (!string.IsNullOrEmpty(_currentBill.Supplier))
            {
                try
                {
                    var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
                    using var sdb = await dbFactory.CreateAsync();
                    var sname = await sdb.QueryFirstOrDefaultAsync<string>(
                        "SELECT name FROM supplier_infor WHERE sid=@S", new { S = _currentBill.Supplier });
                    txtSupplierName.Text = sname ?? _currentBill.Supplier;
                }
                catch { txtSupplierName.Text = _currentBill.Supplier; }
            }

            // 采购员名称
            if (!string.IsNullOrEmpty(_currentBill.Worker))
            {
                try
                {
                    var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
                    using var wdb = await dbFactory.CreateAsync();
                    var wname = await wdb.QueryFirstOrDefaultAsync<string>(
                        "SELECT name FROM work_infor WHERE workid=@W", new { W = _currentBill.Worker });
                    txtWorkerName.Text = wname ?? _currentBill.Worker;
                }
                catch { txtWorkerName.Text = _currentBill.Worker; }
            }

            var statusText = _currentBill.Flag switch { 0 => "草稿", 1 => "已入库", 2 => "已退货", 3 => "已作废", _ => "未知" };
            txtStatus.Text = $"状态: {statusText} | 金额: {_currentBill.Total:N2}";

            Details.Clear();
            var details = await _buyRepo.GetDetailsAsync(sn);
            foreach (var d in details) Details.Add(d);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载采购单失败");
            MessageBox.Show($"加载失败: {ex.Message}", "错误");
        }
    }

    private async void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBill == null) { MessageBox.Show("请先加载单据", "提示"); return; }
        try
        {
            var billData = new BillPrintData
            {
                BillType = "采购",
                Sn = _currentBill.Sn,
                DateText = _currentBill.Datetime?.ToString("yyyy-MM-dd") ?? "",
                PartnerName = txtSupplierName.Text,
                WorkerName = txtWorkerName.Text,
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
                    PartNo = d.Partno,
                    PartName = d.Name,
                    Cartype = d.Cartype ?? "",
                    Unit = d.Unit ?? "",
                    Price = d.Inprice ?? 0,
                    PfPrice = d.Pfprice ?? 0,
                    BillPrice = 0,
                    Amount = (int)(d.Amount ?? 0),
                    Subtotal = d.Stotal ?? 0,
                    Place = d.Place ?? "",
                    Area = "",
                    Brand = "",
                    DiscountRate = 0,
                    Memo = d.Memo
                });
            }

            var dlg = new PrintPreviewWindow(billData, $"采购单-{_currentBill.Sn}")
            {
                Owner = this
            };
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打印预览失败: {ex.Message}", "错误");
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    #region 备注编辑

    /// <summary>进入备注单元格编辑时记录原值，用于判断是否真的修改过</summary>
    private void DgDetails_BeginningEdit(object sender, System.Windows.Controls.DataGridBeginningEditEventArgs e)
    {
        _editingMemoOriginal = null;
        if (IsMemoColumn(e.Column) && e.Row.Item is DetailBuy d)
            _editingMemoOriginal = d.Memo;
    }

    /// <summary>备注单元格提交（回车/焦点离开）后保存到 detail_buy.memo，未变化不写库</summary>
    private async void DgDetails_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != System.Windows.Controls.DataGridEditAction.Commit) return;
        if (!IsMemoColumn(e.Column) || e.Row.Item is not DetailBuy detail) return;

        var newText = (e.EditingElement as System.Windows.Controls.TextBox)?.Text;
        if (newText == _editingMemoOriginal) return;

        try
        {
            await _buyRepo.UpdateDetailMemoAsync(detail.Id, newText);
            detail.Memo = newText;
            _editingMemoOriginal = null;
            txtStatus.Text = $"明细备注已保存: {detail.Partno}";
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "保存明细备注失败 id={Id}", detail.Id);
            MessageBox.Show($"保存明细备注失败: {ex.Message}", "错误");
        }
    }

    private static bool IsMemoColumn(System.Windows.Controls.DataGridColumn column)
        => (column.Header as string) == "备注";

    private string? _editingMemoOriginal;

    /// <summary>单据头备注回车保存到 bill_buy.memo</summary>
    private async void TxtMemo_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _currentBill?.Sn is not string sn) return;
        e.Handled = true;
        var memo = txtMemo.Text;
        if (memo == (_currentBill.Memo ?? "")) return;
        try
        {
            await _buyRepo.UpdateBillMemoAsync(sn, memo);
            _currentBill.Memo = memo;
            txtStatus.Text = "单据备注已保存";
            BillMemoSaved?.Invoke(sn, memo);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "保存单据备注失败 sn={Sn}", _currentBill.Sn);
            MessageBox.Show($"保存单据备注失败: {ex.Message}", "错误");
        }
    }

    #endregion
}
