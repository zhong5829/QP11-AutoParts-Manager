using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QP11.Core.Interfaces;
using System.Windows.Controls.Primitives;
using QP11.Core.Models;

namespace QP11.Wpf.Views;

public partial class StockAlertWindow : Window
{
    private readonly IPartRepository _partRepo;
    private readonly IPartQueryService _partQuery;
    private List<StockAlertItem> _allAlertParts = [];
    private bool _hideWaste = true; // 默认隐藏废品仓

    // 防抖定时器
    private System.Windows.Threading.DispatcherTimer? _debounceTimer;

    public StockAlertWindow(IPartRepository partRepo, IPartQueryService partQuery)
    {
        InitializeComponent();
        _partRepo = partRepo;
        _partQuery = partQuery;
        dgAlert.ItemsSource = new ObservableCollection<StockAlertItem>();

        // 初始化防抖定时器（300ms）
        _debounceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounceTimer.Tick += DebounceTimer_Tick;

        txtPartNo.TextChanged += Field_TextChanged;
        txtName.TextChanged += Field_TextChanged;
        txtCartype.TextChanged += Field_TextChanged;

        // 方向键切换输入框
        _searchFields = [txtPartNo, txtName, txtCartype];
        foreach (var tb in _searchFields)
        {
            tb.PreviewKeyDown += SearchField_PreviewKeyDown;
            tb.GotFocus += SearchField_GotFocus;
        }

        Loaded += async (_, _) => await LoadAlertDataAsync();
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        // 查询按钮：重新从数据库加载 + 应用当前筛选
        _ = LoadAlertDataAsync();
    }

    private async Task LoadAlertDataAsync()
    {
        try
        {
            var data = await _partRepo.GetStockAlertItemsAsync();
            _allAlertParts = data.ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "查询库存预警失败");
            MessageBox.Show($"查询失败: {ex.Message}", "错误");
        }
    }

    private void Field_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
    }

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer?.Stop();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var kwPartNo = txtPartNo.Text.Trim();
        var kwName = txtName.Text.Trim();
        var kwCartype = txtCartype.Text.Trim();

        var filtered = _allAlertParts.AsEnumerable();
        if (_hideWaste)
            filtered = filtered.Where(i => !string.Equals(i.Place, "废品仓", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(kwPartNo))
            filtered = filtered.Where(i => i.Partno?.Contains(kwPartNo, StringComparison.OrdinalIgnoreCase) == true);
        if (!string.IsNullOrEmpty(kwName))
            filtered = filtered.Where(i =>
                i.Name?.Contains(kwName, StringComparison.OrdinalIgnoreCase) == true ||
                i.NamePy?.Contains(kwName, StringComparison.OrdinalIgnoreCase) == true);
        if (!string.IsNullOrEmpty(kwCartype))
            filtered = filtered.Where(i =>
                i.Cartype?.Contains(kwCartype, StringComparison.OrdinalIgnoreCase) == true ||
                i.CartypePy?.Contains(kwCartype, StringComparison.OrdinalIgnoreCase) == true);

        var list = filtered.ToList();
        dgAlert.ItemsSource = new ObservableCollection<StockAlertItem>(list);
        txtCount.Text = $"共 {list.Count} 条预警记录";
    }

    private TextBox[] _searchFields;

    private void SearchField_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (e.Key != System.Windows.Input.Key.Left && e.Key != System.Windows.Input.Key.Right) return;

        int idx = Array.IndexOf(_searchFields, tb);
        if (idx < 0) return;

        int nextIdx = e.Key == System.Windows.Input.Key.Left ? idx - 1 : idx + 1;
        if (nextIdx < 0 || nextIdx >= _searchFields.Length) return;

        // 仅在光标到边界时切换：左键在起始位置，右键在末尾位置
        if (e.Key == System.Windows.Input.Key.Left && tb.CaretIndex > 0) return;
        if (e.Key == System.Windows.Input.Key.Right && tb.CaretIndex < tb.Text.Length) return;

        e.Handled = true;
        var target = _searchFields[nextIdx];
        target.Focus();
        target.SelectAll();
    }

    private static void SearchField_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb) tb.SelectAll();
    }

    private async void DgAlert_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Column.Header as string != "预警值") return;

        var item = e.Row.Item as StockAlertItem;
        if (item == null) return;

        var textBox = e.EditingElement as TextBox;
        if (textBox == null) return;

        if (!decimal.TryParse(textBox.Text, out var newWarning) || newWarning < 0)
        {
            MessageBox.Show("请输入有效的预警值（≥0）", "提示");
            return;
        }

        try
        {
            await _partRepo.UpdateWarningAsync(item.PartId, newWarning);
            item.Warning = newWarning;

            // 同步更新_allAlertParts中的值
            var source = _allAlertParts.FirstOrDefault(x => x.PartId == item.PartId);
            if (source != null) source.Warning = newWarning;

            Serilog.Log.Information("更新预警值: PartId={PartId}, Warning={Warning}", item.PartId, newWarning);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "保存预警值失败");
            MessageBox.Show($"保存失败: {ex.Message}", "错误");
        }
    }

    private void MenuHideWaste_Click(object sender, RoutedEventArgs e)
    {
        var mi = (MenuItem)sender;
        _hideWaste = mi.IsChecked;
        ApplyFilter();
    }

    private void DgAlert_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // 仅在右键仓位列头时弹出菜单
        if (e.OriginalSource is not DependencyObject dep) return;
        var header = FindAncestor<DataGridColumnHeader>(dep);
        if (header?.Column?.Header as string != "仓位") return;

        e.Handled = true;
        var menu = new ContextMenu();
        var mi = new MenuItem { Header = "隐藏废品仓", IsCheckable = true, IsChecked = _hideWaste };
        mi.Click += MenuHideWaste_Click;
        menu.Items.Add(mi);
        menu.IsOpen = true;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T result) return result;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void DgAlert_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.F4) return;

        var item = dgAlert.SelectedItem as StockAlertItem;
        if (item == null) return;

        e.Handled = true;
        var win = new PartHistoryWindow(_partQuery, item.PartId, item.Partno ?? "", item.Name);
        // StockAlertWindow可能通过WindowHostControl嵌入，未Show过不能设Owner
        if (IsLoaded) win.Owner = this;
        win.ShowDialog();
    }
}
