using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using QP11.Core.Interfaces;
using QP11.Services;

namespace QP11.Wpf.Views;

public class StockCheckItem : INotifyPropertyChanged
{
    public string? Partno { get; set; }
    public string? Name { get; set; }
    public string? Unit { get; set; }
    public string? Cartype { get; set; }
    public string? CartypePy { get; set; }
    public long Partid { get; set; }
    public decimal? Lsprice { get; set; }

    private decimal _actualStock;
    public decimal ActualStock
    {
        get => _actualStock;
        set { _actualStock = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiffStock)); OnPropertyChanged(nameof(DiffAmount)); }
    }

    private decimal _systemStock;
    public decimal SystemStock
    {
        get => _systemStock;
        set { _systemStock = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiffStock)); OnPropertyChanged(nameof(DiffAmount)); }
    }

    public decimal DiffStock => ActualStock - SystemStock;

    public decimal DiffAmount => Math.Round(DiffStock * (Lsprice ?? 0m), 2);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class StockCheckWindow : Window
{
    private readonly IPartRepository _partRepo;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly ExportService _exportService;
    public ObservableCollection<StockCheckItem> Items { get; } = [];

    // 搜索防抖
    private System.Windows.Threading.DispatcherTimer? _debounceTimer;

    public StockCheckWindow(IPartRepository partRepo, IDbConnectionFactory dbFactory, IUnitOfWorkFactory uowFactory, ExportService exportService)
    {
        InitializeComponent();
        _partRepo = partRepo;
        _dbFactory = dbFactory;
        _uowFactory = uowFactory;
        _exportService = exportService;
        dgStock.ItemsSource = Items;
        Items.CollectionChanged += (s, e) => UpdateSummary();

        // 初始化防抖定时器（300ms）
        _debounceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounceTimer.Tick += DebounceTimer_Tick;
    }

    private async void BtnLoadStock_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Items.Clear();
            using var db = await _dbFactory.CreateAsync();
            var sql = @"SELECT p.partid AS Partid, p.partno AS Partno, p.name AS Name, p.unit AS Unit,
                        p.cartype AS Cartype, p.cartype_py AS CartypePy,
                        ISNULL(s.amount,0) AS SystemStock, p.lsprice AS Lsprice
                        FROM part_data p
                        LEFT JOIN part_stock s ON p.partid = s.partid
                        WHERE (p.DEL IS NULL OR p.DEL = '0')
                        ORDER BY p.partid";
            var data = await Dapper.SqlMapper.QueryAsync<StockCheckItem>(db, sql);
            foreach (var item in data)
            {
                // 实盘数量默认等于系统库存，用户只需修改有差异的行
                item.ActualStock = item.SystemStock;
                Items.Add(item);
            }
            txtCount.Text = $"共 {Items.Count} 条记录";
            UpdateSummary();
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载盘点数据失败"); MessageBox.Show($"加载失败: {ex.Message}", "错误"); }
    }

    private void UpdateSummary()
    {
        var diffItems = Items.Where(i => i.DiffStock != 0).ToList();
        txtDiffSummary.Text = $"差异: {diffItems.Count}项 | 金额: {diffItems.Sum(i => i.DiffAmount):C2}";
    }

    #region 搜索（防抖 + ICollectionView 过滤，不破坏数据绑定）

    private void SearchField_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
    }

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer?.Stop();
        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        var view = CollectionViewSource.GetDefaultView(Items);
        var kwPartno = txtPartno.Text.Trim().ToLower();
        var kwName = txtName.Text.Trim().ToLower();
        var kwCartype = txtCartype.Text.Trim().ToLower();

        if (string.IsNullOrEmpty(kwPartno) && string.IsNullOrEmpty(kwName) && string.IsNullOrEmpty(kwCartype))
        {
            view.Filter = null;
        }
        else
        {
            // 车型：纯ASCII匹配cartype_py，含中文匹配cartype
            var isCartypeAscii = !string.IsNullOrEmpty(kwCartype) && kwCartype.All(c => c < 128);
            view.Filter = obj => obj is StockCheckItem i &&
                (string.IsNullOrEmpty(kwPartno) || i.Partno?.ToLower().Contains(kwPartno) == true) &&
                (string.IsNullOrEmpty(kwName) || i.Name?.ToLower().Contains(kwName) == true) &&
                (string.IsNullOrEmpty(kwCartype) ||
                    (isCartypeAscii ? i.CartypePy?.ToLower().Contains(kwCartype) == true
                                   : i.Cartype?.ToLower().Contains(kwCartype) == true));
        }
    }

    #endregion

    private async void BtnSaveCheck_Click(object sender, RoutedEventArgs e)
    {
        var diffItems = Items.Where(i => i.DiffStock != 0).ToList();
        if (diffItems.Count == 0) { MessageBox.Show("无差异需要保存", "提示"); return; }

        if (MessageBox.Show($"确认保存 {diffItems.Count} 项盘点差异?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        try
        {
            using var uow = _uowFactory.Create();
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;
            var dbConn = uow.Connection;

            foreach (var item in diffItems)
            {
                if (item.DiffStock > 0)
                    await _partRepo.IncreaseStockAsync(item.Partid, item.DiffStock, txn, dbConn);
                else
                    await _partRepo.DecreaseStockAsync(item.Partid, Math.Abs(item.DiffStock), txn, dbConn);
            }

            await uow.CommitAsync();

            // 审计日志
            foreach (var item in diffItems)
            {
                Serilog.Log.Information("盘点调整: PartId={PartId}, Partno={Partno}, 系统库存={SystemStock}, 实盘={ActualStock}, 差异={Diff}, 操作人={Operator}",
                    item.Partid, item.Partno, item.SystemStock, item.ActualStock, item.DiffStock, App.CurrentUser?.Username ?? "unknown");
            }

            MessageBox.Show("盘点结果已保存，库存已调整", "提示");
            BtnLoadStock_Click(sender, e);
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "保存盘点结果失败"); MessageBox.Show($"保存失败: {ex.Message}", "错误"); }
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (Items.Count == 0) { MessageBox.Show("无数据可导出", "提示"); return; }
        try
        {
            var exportItems = Items.Select(i => new
            {
                件号 = i.Partno,
                名称 = i.Name,
                车型 = i.Cartype,
                单位 = i.Unit,
                系统库存 = i.SystemStock,
                实盘数量 = i.ActualStock,
                差异数量 = i.DiffStock,
                单价 = i.Lsprice,
                差异金额 = i.DiffAmount
            }).ToList();
            var path = await _exportService.ExportToExcelAsync(exportItems, $"盘点结果_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                "件号", "名称", "车型", "单位", "系统库存", "实盘数量", "差异数量", "单价", "差异金额");
            MessageBox.Show($"导出成功!\n文件: {path}", "提示");
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "导出盘点结果失败"); MessageBox.Show($"导出失败: {ex.Message}", "错误"); }
    }
}
