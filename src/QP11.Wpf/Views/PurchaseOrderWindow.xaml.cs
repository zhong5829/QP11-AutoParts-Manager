using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dapper;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace QP11.Wpf.Views;

/// <summary>
/// 计划订货明细行（UI绑定用）
/// </summary>
public class JhdhDetailItem : INotifyPropertyChanged
{
    public long PartId { get; set; }
    public string? PartNo { get; set; }
    public string? Name { get; set; }
    public string? Carname { get; set; }
    public string? Cartype { get; set; }
    public string? Unit { get; set; }
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
        set => _lsPrice = value;
    }

    private decimal _pfPrice;
    public decimal PfPrice
    {
        get => _pfPrice;
        set => _pfPrice = value;
    }

    private decimal _amount = 1;
    public decimal Amount
    {
        get => _amount;
        set { _amount = value; OnPropertyChanged(nameof(Amount)); OnPropertyChanged(nameof(SubTotal)); }
    }

    public decimal SubTotal => Math.Round(InPrice * Amount, 2);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 计划订货单据列表行
/// </summary>
public class JhdhBillDisplay : INotifyPropertyChanged
{
    public string? Sn { get; set; }
    public DateTime? Datetime { get; set; }
    public string? SupplierName { get; set; }
    public string? WorkerName { get; set; }

    private decimal? _total;
    public decimal? Total
    {
        get => _total;
        set { _total = value; OnPropertyChanged(nameof(Total)); }
    }

    public string? FlagText { get; set; }
    public int Flag { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class PurchaseOrderWindow : Window
{
    private readonly UserInfor? _currentUser;
    private readonly IJhdhRepository _jhdhRepo;
    private readonly IJhdhService _jhdhService;
    private readonly IPartRepository _partRepo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly IUserRepository _userRepo;
    private readonly IDbConnectionFactory _dbFactory;

    public ObservableCollection<JhdhDetailItem> Details { get; } = new();

    private BillJhdh? _currentBill;
    private int _currentFlag;
    private bool _initialized;
    private List<SupplierInfor> _allSuppliers = new();

    public PurchaseOrderWindow(
        UserInfor? user,
        IJhdhRepository jhdhRepo,
        IJhdhService jhdhService,
        IPartRepository partRepo,
        ISupplierRepository supplierRepo,
        IUserRepository userRepo,
        IDbConnectionFactory dbFactory)
    {
        _currentUser = user;
        _jhdhRepo = jhdhRepo;
        _jhdhService = jhdhService;
        _partRepo = partRepo;
        _supplierRepo = supplierRepo;
        _userRepo = userRepo;
        _dbFactory = dbFactory;

        InitializeComponent();
        dtBillDate.SelectedDate = DateTime.Now;
        dgDetails.ItemsSource = Details;
        Details.CollectionChanged += OnDetailsChanged;
        _currentFlag = 0;
        _initialized = true;

        LoadDropdowns();
        LoadBillList();
    }

    private void OnDetailsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in e.NewItems?.Cast<JhdhDetailItem>() ?? Array.Empty<JhdhDetailItem>())
            item.PropertyChanged += (_, _) => UpdateTotal();
        UpdateTotal();
    }

    private async void LoadDropdowns()
    {
        try
        {
            _allSuppliers = (await _supplierRepo.GetAllAsync()).ToList();
            cboSupplier.SetSuppliers(_allSuppliers);

            var users = await _userRepo.GetAllAsync();
            cboWorker.ItemsSource = users;
            cboWorker.DisplayMemberPath = "Name";
            cboWorker.SelectedValuePath = "Username";

            var currentUsername = _currentUser?.Username;
            if (!string.IsNullOrEmpty(currentUsername))
            {
                var currentUser = users.FirstOrDefault(u => u.Username == currentUsername);
                if (currentUser != null)
                    cboWorker.SelectedItem = currentUser;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "计划订货-LoadDropdowns 失败");
        }
    }

    private async void LoadBillList()
    {
        try
        {
            using var db = await _dbFactory.CreateAsync();
            var sql = @"SELECT j.sn AS Sn, j.datetime AS Datetime,
                        j.supplier AS Supplier, j.worker AS Worker,
                        j.total AS Total, j.flag AS Flag,
                        ISNULL(s.name, j.supplier) AS SupplierName,
                        ISNULL(w.name, j.worker) AS WorkerName
                        FROM bill_jhdh j
                        LEFT JOIN supplier_infor s ON s.sid = j.supplier
                        LEFT JOIN work_infor w ON w.workid = j.worker
                        WHERE j.flag = @Flag
                        ORDER BY j.datetime DESC";

            var data = (await db.QueryAsync<dynamic>(sql, new { Flag = _currentFlag })).ToList();
            var display = data.Select(b => new JhdhBillDisplay
            {
                Sn = (string?)b.Sn,
                Datetime = (DateTime?)b.Datetime,
                SupplierName = (string?)b.SupplierName,
                WorkerName = (string?)b.WorkerName,
                Total = (decimal?)b.Total,
                Flag = (int?)b.Flag ?? 0,
                FlagText = ((int?)b.Flag ?? 0) switch
                {
                    0 => "未执行",
                    1 => "已执行",
                    2 => "已作废",
                    _ => "未知"
                }
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
        if (!_initialized) return;
        if (rbDraft.IsChecked == true) _currentFlag = 0;
        else if (rbExecuted.IsChecked == true) _currentFlag = 1;
        else if (rbVoided.IsChecked == true) _currentFlag = 2;

        ClearBill();
        LoadBillList();
    }

    private async void DgBills_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (dgBills.SelectedItem is not JhdhBillDisplay row) return;
        if (string.IsNullOrEmpty(row.Sn)) return;

        try
        {
            _currentBill = await _jhdhRepo.GetBySnAsync(row.Sn);
            if (_currentBill == null) return;

            txtBillNo.Text = _currentBill.Sn;
            dtBillDate.SelectedDate = _currentBill.Datetime;
            txtTotal.Text = _currentBill.Total?.ToString("N2") ?? "0";
            txtMemo.Text = _currentBill.Memo ?? "";
            txtFlag.Text = row.FlagText;

            var supplier = _allSuppliers.FirstOrDefault(s => s.Sid == _currentBill.Supplier);
            if (supplier != null)
                cboSupplier.SetSupplier(supplier);
            else
                cboSupplier.SearchText = _currentBill.Supplier ?? "";

            var workerName = await GetWorkerNameAsync(_currentBill.Worker ?? "");
            cboWorker.Text = workerName;

            // 加载明细
            Details.Clear();
            var details = await _jhdhRepo.GetDetailsAsync(row.Sn);
            foreach (var d in details)
            {
                Details.Add(new JhdhDetailItem
                {
                    PartId = d.Partid ?? 0,
                    PartNo = d.Partno,
                    Name = d.Name,
                    Cartype = d.Cartype,
                    Unit = d.Unit,
                    InPrice = d.Price ?? 0m,
                    LsPrice = d.Lsprice ?? 0m,
                    PfPrice = d.Pfprice ?? 0m,
                    Amount = d.Amount ?? 0,
                    Memo = d.Memo
                });
            }
            UpdateTotal();

            // 已执行/已作废的不可编辑
            var isReadOnly = row.Flag == 1 || row.Flag == 2; // 已执行或已作废
            SetHeaderReadOnly(isReadOnly);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载单据失败: {ex.Message}", "错误");
        }
    }

    private async Task<string> GetWorkerNameAsync(string workid)
    {
        if (string.IsNullOrEmpty(workid)) return "";
        using var db = await _dbFactory.CreateAsync();
        return await db.QueryFirstOrDefaultAsync<string>(
            "SELECT name FROM work_infor WHERE workid=@Id", new { Id = workid }) ?? workid;
    }

    private void UpdateTotal()
    {
        var total = Details.Sum(d => d.SubTotal);
        txtTotal.Text = total.ToString("N2");

        // 同步更新右侧单据列表当前行的金额（INotifyPropertyChanged自动刷新DataGrid）
        if (dgBills.SelectedItem is JhdhBillDisplay row)
            row.Total = total;
    }

    private void SetHeaderReadOnly(bool isReadOnly)
    {
        cboSupplier.IsEnabled = !isReadOnly;
        cboWorker.IsEnabled = !isReadOnly;
        txtMemo.IsReadOnly = isReadOnly;
        dgDetails.IsReadOnly = isReadOnly;
    }

    private void ClearBill()
    {
        _currentBill = null;
        txtBillNo.Text = "";
        dtBillDate.SelectedDate = DateTime.Now;
        txtTotal.Text = "0";
        txtMemo.Text = "";
        txtFlag.Text = "";
        cboSupplier.SearchText = "";
        Details.Clear();
        SetHeaderReadOnly(false);
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        ClearBill();
        cboSupplier.Focus();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(cboSupplier.SearchText.Trim()))
        {
            MessageBox.Show("请先选择供应商", "提示");
            cboSupplier.Focus();
            return;
        }

        var selector = new PartSelectorWindow(App.ServiceProvider.GetRequiredService<IPartRepository>(), App.ServiceProvider.GetRequiredService<IPartQueryService>())
        {
            PurchaseMode = true,
            ExistingPartAmounts = Details.Where(d => d.PartId > 0).ToDictionary(d => d.PartId, d => d.Amount)
        };

        // 双击配件时实时添加到明细
        selector.ItemAdded += result =>
        {
            var existing = Details.FirstOrDefault(d => d.PartId == result.PartId);
            if (existing != null)
            {
                existing.Amount += result.Amount;
            }
            else
            {
                Details.Add(new JhdhDetailItem
                {
                    PartId = result.PartId,
                    PartNo = result.PartNo,
                    Name = result.PartName,
                    Carname = result.CarName,
                    Cartype = result.Cartype,
                    Unit = result.Unit,
                    InPrice = result.InPrice,
                    LsPrice = result.LsPrice,
                    PfPrice = result.PfPrice,
                    Amount = result.Amount,
                });
            }
            UpdateTotal();
        };

        var owner = Window.GetWindow(this);
        if (owner != null && owner.IsLoaded)
            selector.Owner = owner;

        // PurchaseMode 下用 Show() 保持窗口打开，用户可连续选择多个配件
        selector.ShowDialog();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (Details.Count == 0)
        {
            MessageBox.Show("请添加计划明细", "提示");
            return;
        }

        if (string.IsNullOrWhiteSpace(cboSupplier.SearchText.Trim()))
        {
            MessageBox.Show("请选择供应商", "提示");
            cboSupplier.Focus();
            return;
        }

        try
        {
            IsEnabled = false;

            var bill = new BillJhdh
            {
                Supplier = cboSupplier.SelectedSupplierId ?? cboSupplier.SearchText.Trim(),
                Worker = cboWorker.SelectedValue as string ?? _currentUser?.Username ?? cboWorker.Text.Trim(),
                Operator = "",
                Datetime = (dtBillDate.SelectedDate?.Date ?? DateTime.Now.Date) + DateTime.Now.TimeOfDay,
                Memo = txtMemo.Text.Trim()
            };

            var detailList = Details.Select(d => new DetailJhdh
            {
                Partid = d.PartId,
                Partno = d.PartNo,
                Name = d.Name,
                Carname = d.Carname,
                Cartype = d.Cartype,
                Unit = d.Unit,
                Price = d.InPrice,
                Pfprice = d.PfPrice,
                Lsprice = d.LsPrice,
                Amount = (long)d.Amount,
                Memo = d.Memo
            }).ToList();

            if (_currentBill != null && !string.IsNullOrEmpty(_currentBill.Sn))
            {
                // 更新已有计划单
                bill.Sn = _currentBill.Sn;
                await _jhdhService.UpdatePlanOrderAsync(bill, detailList);
                MessageBox.Show($"计划单更新成功!\n单号: {bill.Sn}", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // 新建计划单
                var billNo = await _jhdhService.CreatePlanOrderAsync(bill, detailList);
                txtBillNo.Text = billNo;
                _currentBill = bill;
                MessageBox.Show($"计划单保存成功!\n单号: {billNo}", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

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

    private async void BtnConvertToBuy_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBill == null || string.IsNullOrEmpty(_currentBill.Sn))
        {
            MessageBox.Show("请先选择要转采购的计划单", "提示");
            return;
        }

        if (_currentBill.Flag == 1)
        {
            MessageBox.Show("该计划单已执行转采购，不可重复操作", "提示");
            return;
        }

        if (_currentBill.Flag == 2)
        {
            MessageBox.Show("该计划单已作废，无法转采购", "提示");
            return;
        }

        if (Details.Count == 0)
        {
            MessageBox.Show("计划单明细为空", "提示");
            return;
        }

        // 弹出转采购编辑窗口，允许修改到货数量/价格/支付方式
        var convertDlg = new JhdhConvertWindow(_currentBill.Sn, Details.ToList(),
            App.ServiceProvider.GetRequiredService<IJhdhService>());
        var convertOwner = Window.GetWindow(this);
        if (convertOwner != null && convertOwner.IsLoaded)
            convertDlg.Owner = convertOwner;
        if (convertDlg.ShowDialog() == true)
        {
            MessageBox.Show($"转采购入库成功!\n采购单号: {convertDlg.BuySn}", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            ClearBill();
            LoadBillList();
        }
    }

    private async void BtnVoid_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBill == null || string.IsNullOrEmpty(_currentBill.Sn))
        {
            MessageBox.Show("请先选择要作废的计划单", "提示");
            return;
        }

        if (_currentBill.Flag == 1)
        {
            MessageBox.Show("已执行的计划单不能作废", "提示");
            return;
        }

        if (MessageBox.Show($"确认作废计划单 {_currentBill.Sn}?", "确认",
            MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        try
        {
            IsEnabled = false;
            await _jhdhService.CancelPlanOrderAsync(_currentBill.Sn);
            MessageBox.Show("计划单已作废", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            ClearBill();
            LoadBillList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"作废失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        // 有选中项只导选中的，否则导全部
        var selectedBills = dgBills.SelectedItems.Cast<JhdhBillDisplay>().ToList();
        var bills = selectedBills.Count > 0
            ? selectedBills
            : dgBills.ItemsSource?.Cast<JhdhBillDisplay>().ToList() ?? new List<JhdhBillDisplay>();
        if (bills.Count == 0)
        {
            MessageBox.Show("当前没有可导出的单据", "提示");
            return;
        }

        var flagLabel = _currentFlag switch { 0 => "未执行", 1 => "已执行", 2 => "已作废", _ => "" };
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出表格",
            Filter = "Excel 文件|*.xlsx",
            FileName = $"计划订货{flagLabel}{DateTime.Now:yyyyMMdd}.xlsx"
        };
        if (dlg.ShowDialog() != true) return;

        // 构建单据表
        var billsTable = new System.Data.DataTable("计划单据");
        billsTable.Columns.Add("单号", typeof(string));
        billsTable.Columns.Add("日期", typeof(string));
        billsTable.Columns.Add("供应商", typeof(string));
        billsTable.Columns.Add("经手人", typeof(string));
        billsTable.Columns.Add("金额", typeof(string));
        billsTable.Columns.Add("状态", typeof(string));

        foreach (var b in bills)
        {
            billsTable.Rows.Add(b.Sn, b.Datetime?.ToString("yyyy-MM-dd"),
                b.SupplierName, b.WorkerName, b.Total?.ToString("N2") ?? "0", b.FlagText);
        }
        var billSum = bills.Sum(b => b.Total ?? 0m).ToString("N2");
        billsTable.Rows.Add($"共{bills.Count}条", "", "", "", billSum, "");

        // 构建明细表（从数据库加载当前状态下所有单据的明细）
        var detailsTable = new System.Data.DataTable("计划明细");
        detailsTable.Columns.Add("单号", typeof(string));
        detailsTable.Columns.Add("配件编码", typeof(string));
        detailsTable.Columns.Add("名称", typeof(string));
        detailsTable.Columns.Add("车型", typeof(string));
        detailsTable.Columns.Add("单位", typeof(string));
        detailsTable.Columns.Add("采购价", typeof(string));
        detailsTable.Columns.Add("数量", typeof(string));
        detailsTable.Columns.Add("小计", typeof(string));

        try
        {
            IsEnabled = false;
            foreach (var b in bills)
            {
                if (string.IsNullOrEmpty(b.Sn)) continue;
                var details = await _jhdhRepo.GetDetailsAsync(b.Sn);
                foreach (var d in details)
                {
                    detailsTable.Rows.Add(d.Sn, d.Partno, d.Name, d.Cartype, d.Unit,
                        d.Price?.ToString("N2") ?? "0", d.Amount?.ToString() ?? "0",
                        d.Total?.ToString("N2") ?? "0");
                }
            }

            var detailSum = detailsTable.Rows.Count > 0
                ? detailsTable.AsEnumerable().Sum(r => decimal.TryParse(r["小计"]?.ToString(), out var v) ? v : 0m).ToString("N2")
                : "0";
            detailsTable.Rows.Add($"共{detailsTable.Rows.Count}条", "", "", "", "", "", "", detailSum);

            var exportSvc = new QP11.Services.ExportService();
            var (path, error) = await exportSvc.ExportMultiSheetAsync(
                System.IO.Path.GetFileName(dlg.FileName),
                (billsTable, "计划单据", new HashSet<int>()),
                (detailsTable, "计划明细", new HashSet<int>()));
            if (error != null)
                MessageBox.Show(error, "导出失败");
            else
                MessageBox.Show($"导出成功：{path}", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "错误");
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
