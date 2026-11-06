using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Wpf.Helpers;

namespace QP11.Wpf.Views;

public partial class BuyEditDialog : Window
{
    private static readonly Serilog.ILogger _log = Serilog.Log.ForContext<BuyEditDialog>();

    /// <summary>配件确认事件：确认后不关闭窗口，通过此事件通知调用方添加配件到明细</summary>
    public event Action<BuyEditDialog>? PartConfirmed;

    /// <summary>确认后关闭窗口（供选配件窗口连续添加流程使用）；默认 false 保持原有的连续录入行为</summary>
    public bool CloseOnConfirm { get; set; }

    /// <summary>进价→数量→零售价→批发价 回车跳转链路（选配件窗口双击打开时启用）</summary>
    public bool PriceEnterChain { get; set; }

    private long _partId;
    private PartData? _currentPart;
    private PartStock? _currentStock;

    // 静态缓存：同一会话内所有 BuyEditDialog 实例共享下拉数据，避免每次打开都查 8 次 DISTINCT
    private static List<string> _cachedPartNos = new();
    private static List<string> _cachedNames = new();
    private static List<string> _cachedCarNames = new();
    private static List<string> _cachedCarTypes = new();
    private static List<string> _cachedUnits = new();
    private static List<string> _cachedAreas = new();
    private static List<string> _cachedClasses = new();
    private static List<string> _cachedPlaces = new();
    private static bool _cacheLoaded;

    /// <summary>配件档案变更（新增/修改）后调用，强制下次打开对话框重新加载下拉缓存</summary>
    public static void InvalidateCache()
    {
        _cacheLoaded = false;
        _cachedPartNos = new List<string>();
        _cachedNames = new List<string>();
        _cachedCarNames = new List<string>();
        _cachedCarTypes = new List<string>();
        _cachedUnits = new List<string>();
        _cachedAreas = new List<string>();
        _cachedClasses = new List<string>();
        _cachedPlaces = new List<string>();
    }

    private List<string> _allPartNos = new();
    private List<string> _allNames = new();
    private List<string> _allCarNames = new();
    private List<string> _allCarTypes = new();
    private List<string> _allUnits = new();
    private List<string> _allAreas = new();
    private List<string> _allClasses = new();
    private List<string> _allPlaces = new();

    private bool _isUpdatingDddw;
    private bool _isAutoFilling;
    private bool _isSelecting; // 防止 SelectionChanged 重入

    // 防重复：记录上次过滤的文本
    private string _lastPartNoText = "";
    private string _lastNameText = "";

    private decimal? _editAmount;
    private decimal? _editInPrice;
    private string? _editPlace;
    private string? _editMemo;

    public long ResultPartId => _partId;
    public string? ResultPartNo => cboPartNo.Text.Trim();
    public string? ResultName => cboName.Text.Trim();
    public string? ResultCarName => cboCarName.Text.Trim();
    public string? ResultCarType => cboCarType.Text.Trim();
    public string? ResultUnit => cboUnit.Text.Trim();
    public string? ResultArea => cboArea.Text.Trim();
    public string? ResultClass => cboClass.Text.Trim();
    public string? ResultPlace => cboPlace.Text.Trim();
    public decimal ResultInPrice => decimal.TryParse(txtInPrice.Text, out var v) ? v : 0;
    public decimal ResultAmount => decimal.TryParse(txtAmount.Text, out var v) ? v : 0;
    public decimal ResultLsPrice => decimal.TryParse(txtLsPrice.Text, out var v) ? v : 0;
    public decimal ResultPfPrice => decimal.TryParse(txtPfPrice.Text, out var v) ? v : 0;
    public string? ResultMemo => txtMemo.Text.Trim();
    public string? ResultPartTh => txtPartTh.Text.Trim();
    public string? ResultPartGg => txtPartGg.Text.Trim();
    public string? ResultPartCclb => txtPartCclb.Text.Trim();
    public bool IsConfirmed { get; private set; }

    public void SetEditValues(decimal amount, decimal inPrice, string? place, string? memo)
    {
        _editAmount = amount;
        _editInPrice = inPrice;
        _editPlace = place;
        _editMemo = memo;
    }

    public BuyEditDialog(long partId = 0, string? partNo = null)
    {
        InitializeComponent();
        _partId = partId;
        if (!string.IsNullOrEmpty(partNo))
            cboPartNo.Text = partNo;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDddwDataAsync();

        if (_partId > 0)
            await LoadPartByIdAsync(_partId);
        else if (!string.IsNullOrEmpty(cboPartNo.Text))
            await LoadPartByNoAsync(cboPartNo.Text.Trim());

        // 键盘录入链路：打开后焦点落在进价并全选，便于直接输入覆盖原值
        if (PriceEnterChain)
            FocusAndSelect(txtInPrice);
    }

    private async Task LoadDddwDataAsync()
    {
        try
        {
            if (!_cacheLoaded)
            {
                var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
                using var db = await dbFactory.CreateAsync();

                _cachedPartNos = (await db.QueryAsync<string>(
                    "SELECT DISTINCT partno FROM part_data WHERE (DEL IS NULL OR DEL='0') AND partno IS NOT NULL ORDER BY partno")).ToList();
                _cachedNames = (await db.QueryAsync<string>(
                    "SELECT DISTINCT name FROM part_data WHERE (DEL IS NULL OR DEL='0') AND name IS NOT NULL ORDER BY name")).ToList();
                _cachedCarNames = (await db.QueryAsync<string>(
                    "SELECT DISTINCT carname FROM part_data WHERE (DEL IS NULL OR DEL='0') AND carname IS NOT NULL ORDER BY carname")).ToList();
                _cachedCarTypes = (await db.QueryAsync<string>(
                    "SELECT DISTINCT cartype FROM part_data WHERE (DEL IS NULL OR DEL='0') AND cartype IS NOT NULL ORDER BY cartype")).ToList();
                _cachedUnits = (await db.QueryAsync<string>(
                    "SELECT DISTINCT unit FROM part_data WHERE (DEL IS NULL OR DEL='0') AND unit IS NOT NULL ORDER BY unit")).ToList();
                _cachedAreas = (await db.QueryAsync<string>(
                    "SELECT DISTINCT area FROM part_data WHERE (DEL IS NULL OR DEL='0') AND area IS NOT NULL ORDER BY area")).ToList();
                _cachedClasses = (await db.QueryAsync<string>(
                    "SELECT DISTINCT [class] FROM part_data WHERE (DEL IS NULL OR DEL='0') AND [class] IS NOT NULL ORDER BY [class]")).ToList();
                _cachedPlaces = (await db.QueryAsync<string>(
                    "SELECT DISTINCT place FROM part_stock WHERE place IS NOT NULL ORDER BY place")).ToList();

                _cacheLoaded = true;
            }
            else
            {
            }

            // 从缓存复制到实例字段（每个实例独立，支持 ICollectionView 过滤）
            _allPartNos = new List<string>(_cachedPartNos);
            _allNames = new List<string>(_cachedNames);
            _allCarNames = new List<string>(_cachedCarNames);
            _allCarTypes = new List<string>(_cachedCarTypes);
            _allUnits = new List<string>(_cachedUnits);
            _allAreas = new List<string>(_cachedAreas);
            _allClasses = new List<string>(_cachedClasses);
            _allPlaces = new List<string>(_cachedPlaces);

            cboPartNo.ItemsSource = _allPartNos;
            cboName.ItemsSource = _allNames;
            cboCarName.ItemsSource = _allCarNames;
            cboCarType.ItemsSource = _allCarTypes;
            cboUnit.ItemsSource = _allUnits;
            cboArea.ItemsSource = _allAreas;
            cboClass.ItemsSource = _allClasses;
            cboPlace.ItemsSource = _allPlaces;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "加载下拉数据失败");
            MessageBox.Show($"加载下拉数据失败: {ex.Message}", "错误");
        }
    }

    private void Dddw_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingDddw || _isAutoFilling || _isSelecting) return;
        if (sender is not ComboBox cbo) return;

        // 配件编号被清空时，重置所有字段
        if (cbo == cboPartNo && string.IsNullOrWhiteSpace(cbo.Text))
        {
            ClearAllFields();
            return;
        }

        // 防重复：同一控件的同一文本不重复过滤
        var currentText = cbo.Text;
        if (cbo == cboPartNo && currentText == _lastPartNoText) return;
        if (cbo == cboName && currentText == _lastNameText) return;
        var list = GetDddwList(cbo);
        if (list != null)
            FilterDddw(cbo, list);
    }

    /// <summary>清空所有配件相关字段</summary>
    private void ClearAllFields()
    {
        _isAutoFilling = true;
        try
        {
            _partId = 0;
            _currentPart = null;
            _currentStock = null;
            cboName.Text = "";
            cboCarName.Text = "";
            cboCarType.Text = "";
            cboUnit.Text = "";
            cboArea.Text = "";
            cboClass.Text = "";
            cboPlace.Text = "";
            txtPartTh.Text = "";
            txtPartGg.Text = "";
            txtPartCclb.Text = "";
            txtInPrice.Text = "";
            txtLsPrice.Text = "";
            txtPfPrice.Text = "";
            imgPart.Source = null;
            dgBuyHistory.ItemsSource = null;
        }
        finally
        {
            _isAutoFilling = false;
        }
    }

    private List<string>? GetDddwList(ComboBox cbo)
    {
        if (cbo == cboPartNo) return _allPartNos;
        if (cbo == cboName) return _allNames;
        if (cbo == cboCarName) return _allCarNames;
        if (cbo == cboCarType) return _allCarTypes;
        if (cbo == cboUnit) return _allUnits;
        if (cbo == cboArea) return _allAreas;
        if (cbo == cboClass) return _allClasses;
        if (cbo == cboPlace) return _allPlaces;
        return null;
    }

    private const int MaxDropDownItems = 50;

    private void FilterDddw(ComboBox cbo, List<string> allItems)
    {
        // 保存原始文本与光标位置：恢复文本时必须用原始值（含空格），
        // 否则输入尾部空格会被 Trim 后的重设吞掉
        var originalText = cbo.Text;
        var text = originalText.Trim();
        var editTb = FindComboBoxTextBox(cbo);
        var caretIndex = editTb?.CaretIndex ?? originalText.Length;
        _isUpdatingDddw = true;
        try
        {
            if (string.IsNullOrEmpty(text) || rbExactMatch.IsChecked == true)
            {
                // 恢复原始完整列表（暂时取消事件防止重入）
                TemporarilyDetachSelectionChanged(cbo, () =>
                {
                    cbo.ItemsSource = allItems;
                    cbo.SelectedItem = null;
                });
                cbo.IsDropDownOpen = false;
                return;
            }

            // 先在源列表上预过滤，限制最多 MaxDropDownItems 条匹配项
            List<string> matches;
            if (rbExact.IsChecked == true)
                matches = allItems.Where(s => s.StartsWith(text, StringComparison.OrdinalIgnoreCase)).Take(MaxDropDownItems + 1).ToList();
            else
                matches = allItems.Where(s => s.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).Take(MaxDropDownItems + 1).ToList();

            if (matches.Count == 0)
            {
                cbo.IsDropDownOpen = false;
                return;
            }

            // 限制显示数量，替换 ItemsSource 为截断列表（暂时取消事件防止重入）
            var displayItems = matches.Count > MaxDropDownItems
                ? matches.Take(MaxDropDownItems).ToList()
                : matches;

            // 替换 ItemsSource 并清除 SelectedItem，防止 ComboBox 自动选中第一项覆盖用户输入
            TemporarilyDetachSelectionChanged(cbo, () =>
            {
                cbo.ItemsSource = displayItems;
                cbo.SelectedItem = null;
            });

            // 恢复用户正在输入的文本（替换 ItemsSource 后 ComboBox 可能清空文本）。
            // 必须恢复原始文本而非 Trim 后文本，否则尾部空格会被吞掉
            if (cbo.Text != originalText)
                cbo.Text = originalText;

            cbo.IsDropDownOpen = true;

            // 展开下拉框后 ComboBox 会自动全选文本，延迟将光标恢复到输入时的位置（中间插入空格后光标不应跳到末尾）
            var capturedText = cbo.Text;
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                if (cbo.IsDropDownOpen && cbo.Text == capturedText)
                {
                    // ComboBox 的 Select 方法不可用，需通过内部 TextBox 设置光标
                    var tb = FindComboBoxTextBox(cbo);
                    if (tb != null)
                        tb.Select(Math.Min(caretIndex, tb.Text.Length), 0);
                }
            }));

            var totalCount = matches.Count > MaxDropDownItems ? $"{MaxDropDownItems}+" : matches.Count.ToString();
        }
        finally
        {
            // 记录本次过滤的文本，防止重复触发
            if (cbo == cboPartNo) _lastPartNoText = text;
            if (cbo == cboName) _lastNameText = text;
            _isUpdatingDddw = false;
        }
    }

    /// <summary>替换 ItemsSource 时暂时取消 SelectionChanged 事件，防止选中项重置触发循环</summary>
    private void TemporarilyDetachSelectionChanged(ComboBox cbo, Action action)
    {
        if (cbo == cboPartNo)
        {
            cboPartNo.SelectionChanged -= CboPartNo_SelectionChanged;
            try { action(); } finally { cboPartNo.SelectionChanged += CboPartNo_SelectionChanged; }
        }
        else
        {
            action();
        }
    }

    /// <summary>查找 ComboBox 内部的 TextBox（IsEditable=true 时才有）</summary>
    private System.Windows.Controls.TextBox? FindComboBoxTextBox(DependencyObject parent)
    {
        if (parent is System.Windows.Controls.TextBox tb) return tb;
        int children = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < children; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            var result = FindComboBoxTextBox(child);
            if (result != null) return result;
        }
        return null;
    }

    private async void CboPartNo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isAutoFilling || _isUpdatingDddw || _isSelecting) return;
        if (cboPartNo.SelectedItem is string selected && !string.IsNullOrEmpty(selected))
        {
            _isSelecting = true;
            try
            {
                await LoadPartByNoAsync(selected);
            }
            finally
            {
                _isSelecting = false;
            }
        }
    }

    private async void CboPartNo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var text = cboPartNo.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                await LoadPartByNoAsync(text);
            }
        }
    }

    private async Task LoadPartByIdAsync(long partId)
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            _currentPart = await db.QueryFirstOrDefaultAsync<PartData>(
                "SELECT * FROM part_data WHERE partid=@Id", new { Id = partId });

            if (_currentPart == null)
            {
                _log.Warning("LoadPartByIdAsync: 未找到 partId={PartId}", partId);
                return;
            }
            _partId = _currentPart.Partid;
            _currentStock = await db.QueryFirstOrDefaultAsync<PartStock>(
                "SELECT * FROM part_stock WHERE partid=@Id", new { Id = partId });
            AutoFillFields();
            await LoadBuyHistoryAsync(partId);
            DisplayImageForPart(_currentPart.Picture);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "加载配件数据失败, partId={PartId}", partId);
            MessageBox.Show($"加载配件数据失败: {ex.Message}", "错误");
        }
    }

    private async Task LoadPartByNoAsync(string partNo)
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            _currentPart = await db.QueryFirstOrDefaultAsync<PartData>(
                "SELECT * FROM part_data WHERE partno=@PartNo AND (DEL IS NULL OR DEL='0')",
                new { PartNo = partNo });

            if (_currentPart == null)
            {
                _log.Warning("LoadPartByNoAsync: 未找到 partNo={PartNo}", partNo);
                // 查不到即视为新配件：重置配件ID，确认时按新配件（PartId=0）处理，
                // 避免沿用上一个配件的 ID 导致明细合并到错误的配件行
                _partId = 0;
                _currentStock = null;
                return;
            }

            _partId = _currentPart.Partid;
            _currentStock = await db.QueryFirstOrDefaultAsync<PartStock>(
                "SELECT * FROM part_stock WHERE partid=@Id", new { Id = _currentPart.Partid });
            AutoFillFields();
            await LoadBuyHistoryAsync(_currentPart.Partid);
            DisplayImageForPart(_currentPart.Picture);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "加载配件数据失败, partNo={PartNo}", partNo);
            MessageBox.Show($"加载配件数据失败: {ex.Message}", "错误");
        }
    }

    private void AutoFillFields()
    {
        if (_currentPart == null) return;

        _isAutoFilling = true;
        try
        {
            cboPartNo.Text = _currentPart.Partno ?? "";
            cboName.Text = _currentPart.Name ?? "";
            cboCarName.Text = _currentPart.Carname ?? "";
            cboCarType.Text = _currentPart.Cartype ?? "";
            cboUnit.Text = _currentPart.Unit ?? "";
            cboArea.Text = _currentPart.Area ?? "";
            cboClass.Text = _currentPart.ClassName ?? "";
            cboPlace.Text = _currentStock?.Place ?? _currentPart.Place ?? "";
            txtPartTh.Text = _currentPart.PartTh ?? "";
            txtPartGg.Text = _currentPart.PartGg ?? "";
            txtPartCclb.Text = _currentPart.PartCclb ?? "";
            txtInPrice.Text = _currentPart.Inprice?.ToString() ?? "";
            txtLsPrice.Text = (_currentStock?.Lsprice ?? _currentPart.Lsprice)?.ToString() ?? "";
            txtPfPrice.Text = (_currentStock?.Pfprice ?? _currentPart.Pfprice)?.ToString() ?? "";

            if (_editAmount.HasValue)
                txtAmount.Text = _editAmount.Value.ToString();
            if (_editInPrice.HasValue)
                txtInPrice.Text = _editInPrice.Value.ToString();
            if (_editPlace != null)
                cboPlace.Text = _editPlace;
            if (_editMemo != null)
                txtMemo.Text = _editMemo;
        }
        finally
        {
            _isAutoFilling = false;
        }
    }

    private async Task LoadBuyHistoryAsync(long partId)
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var sql = @"SELECT detail_buy.datetime AS Datetime, detail_buy.amount AS Amount,
                        detail_buy.inprice AS Inprice, supplier_infor.name AS Supplier
                        FROM detail_buy
                        LEFT JOIN bill_buy ON detail_buy.sn=bill_buy.sn
                        LEFT JOIN supplier_infor ON bill_buy.supplier=supplier_infor.sid
                        WHERE detail_buy.partid=@PartId
                        ORDER BY detail_buy.datetime DESC";
            var history = await db.QueryAsync<dynamic>(sql, new { PartId = partId });
            var historyList = history.ToList();
            dgBuyHistory.ItemsSource = historyList;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "加载采购历史失败, partId={PartId}", partId);
        }
    }

    private void DisplayImage(byte[]? picture)
    {
        if (picture == null || picture.Length == 0)
        {
            imgPart.Source = null;
            return;
        }
        try
        {
            imgPart.Source = ImageCompressor.LoadBitmap(picture);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "加载配件图片失败");
        }
    }

    /// <summary>切换配件时显示档案图片，并清除上一次上传的压缩提示</summary>
    private void DisplayImageForPart(byte[]? picture)
    {
        txtImgInfo.Visibility = Visibility.Collapsed;
        DisplayImage(picture);
    }

    /// <summary>
    /// 金额录入链路的回车跳转：进价 → 数量 → 零售价 → 批发价 → 确定。
    /// 用 PreviewKeyDown 拦截，避免窗口默认按钮(IsDefault)抢先触发确定。
    /// 未启用链路时保持原有回车行为。
    /// </summary>
    private void PriceBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!PriceEnterChain || e.Key != Key.Enter) return;

        e.Handled = true;

        if (sender == txtInPrice)
            FocusAndSelect(txtAmount);
        else if (sender == txtAmount)
            FocusAndSelect(txtLsPrice);
        else if (sender == txtLsPrice)
            FocusAndSelect(txtPfPrice);
        else
            BtnConfirm_Click(this, new RoutedEventArgs());
    }

    /// <summary>聚焦输入框并全选内容，便于直接输入覆盖</summary>
    private static void FocusAndSelect(TextBox box)
    {
        box.Focus();
        box.SelectAll();
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(cboPartNo.Text))
        {
            MessageBox.Show("请输入或选择配件编号", "提示");
            cboPartNo.Focus();
            return;
        }

        // 必填项：分类（单位不强制）
        if (string.IsNullOrWhiteSpace(cboClass.Text))
        {
            MessageBox.Show("请输入分类", "提示");
            cboClass.Focus();
            return;
        }

        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show("请输入有效的采购数量（必须大于 0）", "提示");
            txtAmount.Focus();
            txtAmount.SelectAll();
            return;
        }

        if (_partId == 0 && _currentPart == null)
        {
            _log.Warning("BtnConfirm_Click: 未找到匹配配件，partno={PartNo}，询问用户是否继续", cboPartNo.Text);
            if (MessageBox.Show("未找到匹配的配件，是否继续?", "提示",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;
        }

        IsConfirmed = true;

        // 触发事件，通知调用方将配件添加到明细
        PartConfirmed?.Invoke(this);

        // 单次录入模式：确认后直接关闭，由调用方读取结果
        if (CloseOnConfirm)
        {
            Close();
            return;
        }

        // 重置窗口为新增状态，继续输入下一个配件
        _isAutoFilling = true;
        try
        {
            _partId = 0;
            _currentPart = null;
            _currentStock = null;
            cboPartNo.Text = "";
            cboName.Text = "";
            cboCarName.Text = "";
            cboCarType.Text = "";
            cboUnit.Text = "";
            cboArea.Text = "";
            cboClass.Text = "";
            cboPlace.Text = "";
            txtPartTh.Text = "";
            txtPartGg.Text = "";
            txtPartCclb.Text = "";
            txtInPrice.Text = "";
            txtLsPrice.Text = "";
            txtPfPrice.Text = "";
            // 数量恢复默认值 1，与窗口首次打开时一致
            txtAmount.Text = "1";
            txtMemo.Text = "";
            imgPart.Source = null;
            txtImgInfo.Visibility = Visibility.Collapsed;
            dgBuyHistory.ItemsSource = null;
        }
        finally
        {
            _isAutoFilling = false;
        }
        cboPartNo.Focus();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        DialogResult = false;
        Close();
    }

    private async void BtnUploadImage_Click(object sender, RoutedEventArgs e)
    {
        if (_partId <= 0)
        {
            MessageBox.Show("请先选择配件", "提示");
            return;
        }

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var raw = File.ReadAllBytes(dlg.FileName);
            // 超过 100KB 自动压缩，与配件编辑窗口保持一致（part_data.picture 统一限 100KB）
            var bytes = ImageCompressor.CompressToLimit(raw, out var compressed);
            if (compressed)
                ShowImageInfo($"已压缩：{ImageCompressor.FormatSize(raw.Length)} → {ImageCompressor.FormatSize(bytes.Length)}");

            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            db.Execute("UPDATE part_data SET picture=@Picture WHERE partid=@Id",
                new { Picture = bytes, Id = _partId });
            DisplayImage(bytes);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "上传图片失败, partId={PartId}", _partId);
            MessageBox.Show($"上传失败: {ex.Message}", "错误");
        }
    }

    /// <summary>上传按钮下方显示图片压缩信息（无信息时隐藏）</summary>
    private void ShowImageInfo(string text)
    {
        txtImgInfo.Text = text;
        txtImgInfo.Visibility = Visibility.Visible;
    }
}
