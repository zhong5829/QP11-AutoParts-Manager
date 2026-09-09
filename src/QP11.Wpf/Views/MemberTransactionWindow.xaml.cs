using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using QP11.Core.Interfaces;
using QP11.Services;

namespace QP11.Wpf.Views;

public partial class MemberTransactionWindow : Window
{
    private readonly IArrearageRepository _arrearageRepo;
    private readonly TransactionOverrideService _overrideService;
    public ObservableCollection<dynamic> TransactionClients { get; } = new();
    public ObservableCollection<MonthlyTransactionRow> MonthlyData { get; } = new();

    /// <summary>缓存已结清勾选状态，key = "sid_year_month"</summary>
    private readonly Dictionary<string, bool> _settledCache = new();

    private static readonly string[] MonthNames = {
        "1月", "2月", "3月", "4月", "5月", "6月",
        "7月", "8月", "9月", "10月", "11月", "12月"
    };

    private string? _currentSid;
    /// <summary>当前月度明细实际所属年份（切换年份时 SaveSettledState 需用它而非下拉当前值）</summary>
    private int _currentYear = DateTime.Now.Year;

    public MemberTransactionWindow(IArrearageRepository arrearageRepo)
    {
        _arrearageRepo = arrearageRepo;
        _overrideService = new TransactionOverrideService();
        InitializeComponent();
        dgClients.ItemsSource = TransactionClients;
        dgMonthly.ItemsSource = MonthlyData;
        InitYearCombo();
        // 预热建表（数据访问前自动确保存储表存在）
        _ = _overrideService.EnsureTableAsync();
        _ = LoadTransactionClientsAsync();
    }

    private void InitYearCombo()
    {
        var currentYear = DateTime.Now.Year;
        for (int y = currentYear; y >= currentYear - 5; y--)
        {
            cboYear.Items.Add(y);
        }
        cboYear.SelectedItem = currentYear;
    }

    private async System.Threading.Tasks.Task LoadTransactionClientsAsync(string? keyword = null, string? preserveSid = null)
    {
        try
        {
            TransactionClients.Clear();
            var year = cboYear.SelectedItem as int? ?? DateTime.Now.Year;
            var data = await _arrearageRepo.GetTransactionClientsAsync(year, keyword);
            foreach (var c in data) TransactionClients.Add(c);

            // 列表重建会清空选中（如切换年份），恢复原选中以触发明细刷新
            if (preserveSid != null)
            {
                var row = TransactionClients.FirstOrDefault(c => (string)c.sid == preserveSid);
                if (row != null) dgClients.SelectedItem = row;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载客户列表失败: {ex.Message}", "错误");
        }
    }

    private void TxtSearchClient_TextChanged(object sender, TextChangedEventArgs e)
    {
        var kw = txtSearchClient.Text.Trim();
        if (kw.Length >= 2) _ = LoadTransactionClientsAsync(kw);
        else if (kw.Length == 0) _ = LoadTransactionClientsAsync();
    }

    private void BtnSearchClient_Click(object sender, RoutedEventArgs e)
        => _ = LoadTransactionClientsAsync(txtSearchClient.Text.Trim());

    private async void DgClients_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var client = dgClients.SelectedItem as dynamic;
        if (client == null) return;
        txtClientName.Text = $"[{client.sid}] {client.name}";
        await LoadMonthlySummary((string)client.sid);
    }

    private async void CboYear_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cboYear.SelectedItem == null) return;
        // 记住当前选中客户，列表重建后恢复选中并刷新月度明细
        var client = dgClients.SelectedItem as dynamic;
        var sid = client != null ? (string)client.sid : null;
        await LoadTransactionClientsAsync(txtSearchClient.Text.Trim(), sid);
    }

    private MonthlyTransactionRow? _lastEditedRow;

    private void DgMonthly_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is MonthlyTransactionRow row)
            _lastEditedRow = row;
    }

    private async void DgMonthly_CurrentCellChanged(object? sender, EventArgs e)
    {
        // 焦点离开编辑单元格后，绑定值已提交，此时重算；仅当值真正变化时才持久化
        var row = _lastEditedRow;
        if (row == null) return;
        _lastEditedRow = null;

        row.RecalcExternal();
        RefreshTotalSummary();
        if (_currentSid != null && row.ChangedFromSaved)
        {
            try
            {
                await _overrideService.SaveOverrideAsync(_currentSid, _currentYear, row.month_num,
                    row.buy_total, row.sell_total, row.is_settled);
                row.MarkSaved();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存往来数据失败: {ex.Message}", "错误");
            }
        }
        _ = Dispatcher.BeginInvoke(() => dgMonthly.Items.Refresh());
    }

    private async System.Threading.Tasks.Task LoadMonthlySummary(string cid)
    {
        if (cboYear.SelectedItem == null) return;
        var year = (int)cboYear.SelectedItem;

        // 切换客户/年份前，保存当前勾选状态到缓存（用当前明细实际年份）
        SaveSettledState();

        _currentSid = cid;
        _currentYear = year;

        try
        {
            MonthlyData.Clear();
            var data = await _arrearageRepo.GetMonthlyTransactionSummaryAsync(cid, year);
            var monthMap = new Dictionary<int, dynamic>();
            foreach (var row in data) monthMap[(int)row.month] = row;

            decimal totalBuy = 0, totalSell = 0;

            // 补全当年 1-12 月，无数据的月份显示 0，保证所有客户月份一致
            for (int monthNum = 1; monthNum <= 12; monthNum++)
            {
                monthMap.TryGetValue(monthNum, out dynamic? dbRow);
                var hasData = dbRow != null;
                var sellTotal = hasData ? (decimal)dbRow!.sell_total : 0m;
                var cacheKey = $"{cid}_{year}_{monthNum}";

                // 进货列默认为0，不自动取数据库值；出货列自动取值
                var buyTotal = 0m;

                // 应用本地覆盖值（覆盖优先级最高）
                var overrideEntry = await _overrideService.GetOverrideAsync(cid, year, monthNum);
                bool isSettled = _settledCache.TryGetValue(cacheKey, out var settled2) && settled2;
                if (overrideEntry != null)
                {
                    buyTotal = overrideEntry.buy_total;
                    sellTotal = overrideEntry.sell_total;
                    isSettled = overrideEntry.is_settled;
                }

                var item = new MonthlyTransactionRow
                {
                    month_num = monthNum,
                    month_name = MonthNames[monthNum - 1],
                    buy_total = buyTotal,
                    sell_total = sellTotal,
                    buy_settled = hasData ? (decimal)dbRow!.buy_settled : 0m,
                    sell_settled = hasData ? (decimal)dbRow!.sell_settled : 0m,
                    is_settled = isSettled
                };
                // 记录当前加载值作为“已保存状态”，后续仅值变化时才写库
                item.MarkSaved();
                item.RowChanged += RefreshTotalSummary;
                item.SettledChanged += () => OnRowSettledChanged(item);
                MonthlyData.Add(item);
                totalBuy += buyTotal;
                totalSell += sellTotal;
            }

            var totalRecvPay = totalSell - totalBuy;
            txtTotalBuy.Text = totalBuy.ToString("N2");
            txtTotalSell.Text = totalSell.ToString("N2");
            UpdateTotalRecvPay(totalRecvPay);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载往来明细失败: {ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 单行数据变更时重新计算底部合计
    /// </summary>
    private void RefreshTotalSummary()
    {
        decimal totalBuy = 0, totalSell = 0;
        foreach (var row in MonthlyData)
        {
            totalBuy += row.buy_total;
            totalSell += row.sell_total;
        }
        txtTotalBuy.Text = totalBuy.ToString("N2");
        txtTotalSell.Text = totalSell.ToString("N2");
        UpdateTotalRecvPay(totalSell - totalBuy);
    }

    private void UpdateTotalRecvPay(decimal totalRecvPay)
    {
        txtTotalRecvPay.Text = totalRecvPay.ToString("N2");
        txtTotalRecvPay.Foreground = totalRecvPay < 0
            ? System.Windows.Media.Brushes.Red
            : System.Windows.Media.Brushes.Green;
    }

    /// <summary>
    /// 勾选已结清变更时，保存到数据库
    /// </summary>
    private async void OnRowSettledChanged(MonthlyTransactionRow row)
    {
        if (_currentSid == null) return;
        try
        {
            await _overrideService.SaveOverrideAsync(_currentSid, _currentYear, row.month_num,
                row.buy_total, row.sell_total, row.is_settled);
            row.MarkSaved();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存已结清状态失败: {ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 将当前月度明细的勾选状态保存到缓存（key 使用明细实际年份 _currentYear，避免下拉已切换导致串年份）
    /// </summary>
    private void SaveSettledState()
    {
        if (_currentSid == null) return;
        foreach (var row in MonthlyData)
        {
            var key = $"{_currentSid}_{_currentYear}_{row.month_num}";
            _settledCache[key] = row.is_settled;
        }
    }
}

/// <summary>
/// 月度往来汇总行模型
/// </summary>
public class MonthlyTransactionRow : INotifyPropertyChanged
{
    public int month_num { get; set; }
    public string? month_name { get; set; }

    private decimal _buy_total;
    public decimal buy_total
    {
        get => _buy_total;
        set { _buy_total = value; Recalc(); OnPropertyChanged(); }
    }

    private decimal _sell_total;
    public decimal sell_total
    {
        get => _sell_total;
        set { _sell_total = value; Recalc(); OnPropertyChanged(); }
    }

    private decimal _recv_pay;
    public decimal recv_pay
    {
        get => _recv_pay;
        set { _recv_pay = value; OnPropertyChanged(); }
    }

    private bool _is_negative;
    public bool is_negative
    {
        get => _is_negative;
        set { _is_negative = value; OnPropertyChanged(); }
    }

    public decimal buy_settled { get; set; }
    public decimal sell_settled { get; set; }

    private bool _is_settled;
    public bool is_settled
    {
        get => _is_settled;
        set { _is_settled = value; OnPropertyChanged(); SettledChanged?.Invoke(); }
    }

    /// <summary>上次写库时的进货/出货/已结清快照（仅值变化时才持久化）</summary>
    public decimal LastBuyTotal { get; private set; }
    public decimal LastSellTotal { get; private set; }
    public bool LastSettled { get; private set; }

    /// <summary>当前值是否与上次写库快照有变化</summary>
    public bool ChangedFromSaved =>
        _buy_total != LastBuyTotal || _sell_total != LastSellTotal || _is_settled != LastSettled;

    /// <summary>将当前值记录为“已写库状态”（加载及保存成功后调用）</summary>
    public void MarkSaved()
    {
        LastBuyTotal = _buy_total;
        LastSellTotal = _sell_total;
        LastSettled = _is_settled;
    }

    /// <summary>勾选状态变更时通知外部持久化</summary>
    public event Action? SettledChanged;

    /// <summary>行数据变更时通知外部刷新合计</summary>
    public event Action? RowChanged;

    private void Recalc()
    {
        var oldNeg = _is_negative;
        _recv_pay = _sell_total - _buy_total;
        _is_negative = _recv_pay < 0;
        OnPropertyChanged(nameof(recv_pay));
        if (oldNeg != _is_negative) OnPropertyChanged(nameof(is_negative));
        RowChanged?.Invoke();
    }

    /// <summary>供外部强制重算（编辑结束后绑定值已更新时调用）</summary>
    public void RecalcExternal() => Recalc();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
