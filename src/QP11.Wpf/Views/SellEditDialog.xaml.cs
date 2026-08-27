using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Dapper;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Wpf.Helpers;

namespace QP11.Wpf.Views;

public partial class SellEditDialog : Window
{
    private readonly ISellRepository _sellRepo;
    private readonly IBuyRepository _buyRepo;
    private readonly IClientRepository _clientRepo;
    private readonly IDbConnectionFactory _dbFactory;

    private readonly long _partId;
    private readonly decimal _lsprice;
    private readonly decimal _pfprice;
    private readonly decimal _stockAmount;
    private readonly bool _readOnly;

    private List<ClientInfor> _allClients = new();
    private bool _syncingPrice;
    private bool _skipClientTextChanged;
    private decimal _lastInprice; // 最后一次进价

    // 左栏输入字段导航链（车牌号 → 车型 → 备注 → 销售数量 → 销售单价 → 开票单价）
    private readonly TextBox[] _inputFields = [];

    // 拼音缓存：客户名 → 拼音首字母，Loaded时一次性算好
    private readonly Dictionary<string, string> _pinyinCache = [];

    // 防抖 + 取消令牌
    private CancellationTokenSource? _cts;
    private System.Windows.Threading.DispatcherTimer? _debounceTimer;

    public decimal Amount { get; private set; }
    public decimal Price { get; private set; }
    public decimal BillPrice { get; private set; }
    public string? CarMark { get; private set; }
    public string? Cartype { get; private set; }
    public string? Memo { get; private set; }
    public string? ClientName { get; private set; }
    public string? ClientCode { get; private set; }
    public bool IsConfirmed { get; private set; }

    public SellEditDialog(
        long partId, string partNo, string partName,
        decimal lsprice, decimal pfprice, decimal stockAmount,
        ISellRepository sellRepo, IBuyRepository buyRepo, IClientRepository clientRepo, IDbConnectionFactory dbFactory,
        string? clientName = null, string cartype = "", bool readOnly = false)
    {
        InitializeComponent();

        _partId = partId;
        _lsprice = lsprice;
        _pfprice = pfprice;
        _stockAmount = stockAmount;
        _readOnly = readOnly;
        _sellRepo = sellRepo;
        _buyRepo = buyRepo;
        _clientRepo = clientRepo;
        _dbFactory = dbFactory;

        txtPartNo.Text = partNo;
        txtPartName.Text = partName;
        txtCartype.Text = cartype;

        rbRetail.IsChecked = true;
        txtPrice.Text = lsprice.ToString();
        txtBillPrice.Text = lsprice.ToString();

        if (!string.IsNullOrEmpty(clientName))
        {
            _skipClientTextChanged = true;
            txtClient.Text = clientName;
            _skipClientTextChanged = false;
        }

        // 初始化左栏输入字段导航链
        _inputFields = [txtCarMark, txtCartype, txtMemo, txtAmount, txtPrice, txtBillPrice];

        txtClient.TextChanged += TxtClient_TextChanged;

        Loaded += SellEditDialog_Loaded;
    }

    private async void SellEditDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _allClients = (await _clientRepo.GetAllAsync()).ToList();
            // 预计算拼音缓存
            foreach (var c in _allClients)
            {
                if (!string.IsNullOrEmpty(c.Name))
                    _pinyinCache[c.Name] = PinyinHelper.GetPinyinInitials(c.Name);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载客户列表失败");
        }

        // 初始化防抖定时器（300ms）
        _debounceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounceTimer.Tick += DebounceTimer_Tick;

        _ = LoadSellHistoryAsync(CancellationToken.None);
        LoadBuyHistoryAsync();

        // 只读模式：禁用所有输入控件，确认按钮置灰
        if (_readOnly)
        {
            Title = $"配件历史记录 - {txtPartNo.Text} {txtPartName.Text}";
            btnOk.IsEnabled = false;
            btnOk.Opacity = 0.5;
            txtAmount.IsReadOnly = true;
            txtPrice.IsReadOnly = true;
            txtBillPrice.IsReadOnly = true;
            txtCarMark.IsReadOnly = true;
            txtCartype.IsReadOnly = true;
            txtMemo.IsReadOnly = true;
            rbRetail.IsEnabled = false;
            rbWholesale.IsEnabled = false;
            chkAutoMatch.IsEnabled = false;
            btnHistory.Visibility = Visibility.Collapsed;

            // 显示库存为0提示
            txtPriceHint.Text = "当前库存为0，仅可查看历史记录";
            txtPriceHint.Foreground = Brushes.Red;
            txtPriceHint.Visibility = Visibility.Visible;
        }
        else
        {
            if (chkAutoMatch.IsChecked == true)
                await TryAutoMatchPriceAsync(CancellationToken.None);

            txtAmount.Focus();
            txtAmount.SelectAll();
        }
    }

    /// <summary>客户输入变化：触发防抖查询历史和价格自动匹配</summary>
    private void TxtClient_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_skipClientTextChanged) return;
        // 防抖：重置定时器，300ms 内无新输入才触发查询
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
    }

    private async void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer?.Stop();

        // 取消上一次未完成的查询
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await LoadSellHistoryAsync(token);
            if (chkAutoMatch.IsChecked == true)
                await TryAutoMatchPriceAsync(token);
        }
        catch (OperationCanceledException)
        {
            // 被新查询取消，正常忽略
        }
    }

    private async Task LoadSellHistoryAsync(CancellationToken ct)
    {
        try
        {
            var input = txtClient.Text.Trim();
            var matchedNames = ResolveClientNames(input);

            using var db = await _dbFactory.CreateAsync();
            string sql;
            object param;

            if (matchedNames.Count > 0)
            {
                sql = @"SELECT detail_sell.sn AS Sn, detail_sell.amount AS Amount,
                        ISNULL(detail_sell.price, 0) AS Price,
                        ISNULL(detail_sell.bill_price, detail_sell.price) AS BillPrice,
                        detail_sell.datetime AS Datetime,
                        CASE WHEN ISNULL(bill_sell.flag, 0) = 3 THEN '配件报损' ELSE client_infor.name END AS ClientName,
                        ISNULL(bill_sell.flag, 0) AS Flag
                        FROM detail_sell
                        LEFT JOIN bill_sell ON bill_sell.sn = detail_sell.sn
                        LEFT JOIN client_infor ON client_infor.cid = bill_sell.client
                        WHERE detail_sell.partid = @PartId
                          AND client_infor.name IN @ClientNames
                        ORDER BY detail_sell.datetime DESC";
                param = new { PartId = _partId, ClientNames = matchedNames };
            }
            else
            {
                sql = @"SELECT detail_sell.sn AS Sn, detail_sell.amount AS Amount,
                        ISNULL(detail_sell.price, 0) AS Price,
                        ISNULL(detail_sell.bill_price, detail_sell.price) AS BillPrice,
                        detail_sell.datetime AS Datetime,
                        CASE WHEN ISNULL(bill_sell.flag, 0) = 3 THEN '配件报损' ELSE client_infor.name END AS ClientName,
                        ISNULL(bill_sell.flag, 0) AS Flag
                        FROM detail_sell
                        LEFT JOIN bill_sell ON bill_sell.sn = detail_sell.sn
                        LEFT JOIN client_infor ON client_infor.cid = bill_sell.client
                        WHERE detail_sell.partid = @PartId
                        ORDER BY detail_sell.datetime DESC";
                param = new { PartId = _partId };
            }

            var data = (await db.QueryAsync<dynamic>(sql, param)).ToList();
            ct.ThrowIfCancellationRequested();
            dgSellHistory.ItemsSource = data;
        }
        catch (OperationCanceledException)
        {
            // 被新查询取消，忽略
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载销售历史失败");
        }
    }

    private async void LoadBuyHistoryAsync()
    {
        try
        {
            using var db = await _dbFactory.CreateAsync();
            var sql = @"SELECT bill_buy.sn AS Sn, detail_buy.amount AS Amount,
                        detail_buy.inprice AS Inprice, detail_buy.datetime AS Datetime,
                        supplier_infor.name AS SupplierName,
                        ISNULL(bill_buy.flag, 0) AS Flag
                        FROM detail_buy
                        LEFT JOIN bill_buy ON bill_buy.sn = detail_buy.sn
                        LEFT JOIN supplier_infor ON supplier_infor.sid = bill_buy.supplier
                        WHERE detail_buy.partid = @PartId
                        ORDER BY detail_buy.datetime DESC";
            var data = (await db.QueryAsync<dynamic>(sql, new { PartId = _partId })).ToList();
            dgBuyHistory.ItemsSource = data;
            // 取最后一次进价（已按日期降序排列）
            if (data.Count > 0 && data[0].Inprice != null)
                _lastInprice = (decimal)data[0].Inprice;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载进货历史失败");
        }
    }

    private void RbRetail_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _syncingPrice = true;
        txtPrice.Text = _lsprice.ToString();
        txtBillPrice.Text = _lsprice.ToString();
        _syncingPrice = false;
    }

    private void RbWholesale_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _syncingPrice = true;
        txtPrice.Text = _pfprice.ToString();
        txtBillPrice.Text = _pfprice.ToString();
        _syncingPrice = false;
    }

    private void TxtPrice_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 改销售单价 → 同步开票单价；单独改开票单价则不受影响
        if (_syncingPrice) return;
        _syncingPrice = true;
        txtBillPrice.Text = txtPrice.Text;
        _syncingPrice = false;
    }

    /// <summary>需求1：输入框获得焦点时自动全选</summary>
    private void InputField_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.SelectAll();
        }
    }

    /// <summary>需求2：上下方向键在输入字段间切换，回车跳下一字段（末字段回车确认）</summary>
    private void InputField_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Up && e.Key != Key.Down && e.Key != Key.Enter) return;
        if (sender is not TextBox current) return;

        var index = Array.IndexOf(_inputFields, current);
        if (index < 0) return;

        if (e.Key == Key.Up)
        {
            if (index <= 0) return;
            e.Handled = true;
            _inputFields[index - 1].Focus();
        }
        else // Down 或 Enter：跳下一字段，末字段回车确认
        {
            if (index < _inputFields.Length - 1)
            {
                e.Handled = true;
                _inputFields[index + 1].Focus();
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                BtnOk_Click(sender, e);
            }
        }
    }

    private async void ChkAutoMatch_Changed(object sender, RoutedEventArgs e)
    {
        if (chkAutoMatch.IsChecked == true)
            await TryAutoMatchPriceAsync(CancellationToken.None);
    }

    private async Task TryAutoMatchPriceAsync(CancellationToken ct)
    {
        var input = txtClient.Text.Trim();
        var matchedNames = ResolveClientNames(input);
        if (matchedNames.Count == 0) return;

        try
        {
            using var db = await _dbFactory.CreateAsync();
            var sql = @"SELECT TOP 1 detail_sell.price, detail_sell.bill_price
                        FROM detail_sell
                        LEFT JOIN bill_sell ON bill_sell.sn = detail_sell.sn
                        LEFT JOIN client_infor ON client_infor.cid = bill_sell.client
                        WHERE detail_sell.partid = @PartId
                          AND client_infor.name IN @ClientNames
                        ORDER BY detail_sell.datetime DESC";
            var row = await db.QueryFirstOrDefaultAsync<dynamic>(sql,
                new { PartId = _partId, ClientNames = matchedNames });
            ct.ThrowIfCancellationRequested();
            if (row != null)
            {
                _syncingPrice = true;
                txtPrice.Text = ((decimal)row.price).ToString();
                txtBillPrice.Text = ((decimal)row.bill_price).ToString();
                _syncingPrice = false;
            }
        }
        catch (OperationCanceledException)
        {
            // 被新查询取消，忽略
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "自动匹配价格失败");
        }
    }

    /// <summary>
    /// 根据输入文本解析所有匹配的客户名列表
    /// 匹配规则：拼音首字母包含匹配 → 精确匹配 → 名称包含匹配 → 原文LIKE匹配
    /// 注意：不再短路返回，合并所有匹配结果，避免脏数据（如 name 为拼音字母的客户）导致拼音匹配被跳过
    /// </summary>
    private List<string> ResolveClientNames(string input)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(input)) return result;

        var matched = new List<string>();

        // 拼音首字母匹配（优先，用户输入拼音时意图是搜索拼音对应客户）
        foreach (var c in _allClients)
        {
            if (string.IsNullOrEmpty(c.Name)) continue;
            // 优先用内存缓存的拼音，如果缓存没有则用数据库 name_py 字段作为后备
            var py = _pinyinCache.GetValueOrDefault(c.Name) ?? c.NamePy ?? "";
            if (!string.IsNullOrEmpty(py) && py.Contains(input, StringComparison.OrdinalIgnoreCase))
                matched.Add(c.Name);
        }

        // 精确匹配客户名
        foreach (var c in _allClients.Where(c => c.Name == input))
            if (!string.IsNullOrEmpty(c.Name) && !matched.Contains(c.Name))
                matched.Add(c.Name);

        // 名称包含匹配
        foreach (var c in _allClients.Where(c => c.Name?.Contains(input, StringComparison.OrdinalIgnoreCase) == true))
            if (!string.IsNullOrEmpty(c.Name) && !matched.Contains(c.Name))
                matched.Add(c.Name);

        if (matched.Count > 0)
            return matched;

        // 都没匹配到，返回原文（SQL会用IN做精确匹配，无结果则无结果）
        result.Add(input);
        return result;
    }

    private string? GetSelectedClientName()
    {
        var names = ResolveClientNames(txtClient.Text.Trim());
        return names.Count > 0 ? names[0] : null;
    }

    private string? GetSelectedClientCode()
    {
        var names = ResolveClientNames(txtClient.Text.Trim());
        if (names.Count == 0) return null;
        // 取第一个匹配客户名对应的Cid（ResolveClientNames 已按拼音优先排序）
        return _allClients.FirstOrDefault(c => c.Name == names[0])?.Cid;
    }

    private async void BtnHistory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var db = await _dbFactory.CreateAsync();
            var sql = @"SELECT MAX(detail_sell.price) AS MaxPrice, MIN(detail_sell.price) AS MinPrice
                        FROM detail_sell
                        WHERE detail_sell.partid = @PartId";
            var row = await db.QueryFirstOrDefaultAsync<dynamic>(sql, new { PartId = _partId });
            if (row != null && (object?)row!.MaxPrice != null)
            {
                txtPriceHint.Visibility = Visibility.Visible;
                var maxP = (decimal)row!.MaxPrice;
                var minP = (object?)row!.MinPrice != null ? (decimal)row!.MinPrice : maxP;
                txtPriceHint.Text = $"最高价:{maxP:N2} 最低价:{minP:N2}";
            }
            else
            {
                txtPriceHint.Visibility = Visibility.Visible;
                txtPriceHint.Text = "暂无历史售价记录";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询历史售价失败: {ex.Message}", "错误");
        }
    }

    /// <summary>双击销售历史行 → 查看整张销售单</summary>
    private void DgSellHistory_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (dgSellHistory.SelectedItem == null) return;
        dynamic row = dgSellHistory.SelectedItem;
        string? sn;
        try { sn = row.Sn as string; } catch { return; }
        if (string.IsNullOrEmpty(sn)) return;

        var win = new OrderDetailWindow(sn, OrderType.Sell);
        win.Owner = this;
        win.Show();
    }

    /// <summary>双击采购历史行 → 查看整张采购单</summary>
    private void DgBuyHistory_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (dgBuyHistory.SelectedItem == null) return;
        dynamic row = dgBuyHistory.SelectedItem;
        string? sn;
        try { sn = row.Sn as string; } catch { return; }
        if (string.IsNullOrEmpty(sn)) return;

        var win = new OrderDetailWindow(sn, OrderType.Buy);
        win.Owner = this;
        win.Show();
    }

    public void SetEditValues(decimal amount, decimal price, decimal billPrice, string? carMark, string? cartype, string? memo = "")
    {
        txtAmount.Text = amount.ToString();
        txtPrice.Text = price.ToString();
        txtBillPrice.Text = billPrice.ToString();
        txtCarMark.Text = carMark ?? "";
        txtCartype.Text = cartype ?? "";
        txtMemo.Text = memo ?? "";
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show("请输入有效的销售数量", "提示");
            txtAmount.Focus();
            return;
        }
        if (amount > _stockAmount && _stockAmount > 0)
        {
            MessageBox.Show($"库存不足！当前库存: {_stockAmount:F0}，已自动调整为1", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            txtAmount.Text = "1";
            txtAmount.Focus();
            txtAmount.SelectAll();
            return;
        }
        if (!decimal.TryParse(txtPrice.Text, out var price) || price < 0)
        {
            MessageBox.Show("请输入有效的销售单价", "提示");
            txtPrice.Focus();
            return;
        }
        if (!decimal.TryParse(txtBillPrice.Text, out var billPrice) || billPrice < 0)
        {
            MessageBox.Show("请输入有效的开票单价", "提示");
            txtBillPrice.Focus();
            return;
        }

        // 售价低于最后一次进价时二次确认
        if (_lastInprice > 0 && price < _lastInprice)
        {
            var confirm = MessageBox.Show(
                $"销售单价 {price:N2} 低于最后一次进价 {_lastInprice:N2}，确定以此价格开单？",
                "价格低于进价", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                txtPrice.Focus();
                txtPrice.SelectAll();
                return;
            }
        }

        Amount = amount;
        Price = price;
        BillPrice = billPrice;
        CarMark = txtCarMark.Text.Trim();
        Cartype = txtCartype.Text.Trim();
        Memo = txtMemo.Text.Trim();
        ClientName = GetSelectedClientName();
        ClientCode = GetSelectedClientCode();
        IsConfirmed = true;

        DialogResult = true;
        Close();
    }
}
