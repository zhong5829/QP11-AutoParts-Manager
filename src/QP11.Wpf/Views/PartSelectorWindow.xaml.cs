using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Wpf.Helpers;

namespace QP11.Wpf.Views;

/// <summary>批量选择结果（含数量、进价）</summary>
public class PartSelectResult
{
    public long PartId { get; set; }
    public string? PartNo { get; set; }
    public string? PartName { get; set; }
    public string? CarName { get; set; }
    public string? Cartype { get; set; }
    public decimal InPrice { get; set; }
    public decimal LsPrice { get; set; }
    public decimal PfPrice { get; set; }
    public decimal Amount { get; set; }
    public string Unit { get; set; } = "";
}

public partial class PartSelectorWindow : Window
{
    private readonly IPartRepository _partRepo;
    private readonly IPartQueryService _partQuery;
    public ObservableCollection<PartData> Parts { get; } = new();
    public ObservableCollection<PartData> SelectedParts { get; } = new();

    /// <summary>采购模式下带回的数量/金额结果</summary>
    public List<PartSelectResult> PurchaseResults { get; } = new();

    /// <summary>已有明细中的配件ID及数量（由调用方传入，用于重复检测和提示）</summary>
    public Dictionary<long, decimal> ExistingPartAmounts { get; set; } = new();

    /// <summary>采购模式：每添加一个配件时的回调（用于实时写入明细）</summary>
    public event Action<PartSelectResult>? ItemAdded;

    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    /// <summary>采购模式：双击弹出数量/金额输入框</summary>
    public bool PurchaseMode { get; set; }

    /// <summary>借货模式：双击复用销售开单配件历史窗口(SellEditDialog)，确认后通过 ItemAdded 实时加入借货明细</summary>
    public bool BorrowMode { get; set; }

    private readonly TextBox[] _queryTextBoxes;
    private bool _hideWaste = true; // 默认隐藏废品仓
    private List<PartData> _allParts = []; // 全量缓存，用于废品仓过滤

    public PartSelectorWindow(IPartRepository partRepo, IPartQueryService partQuery)
    {
        _partRepo = partRepo;
        _partQuery = partQuery;
        InitializeComponent();
        dgParts.ItemsSource = Parts;
        _queryTextBoxes = new[] { txtPartNo, txtPartName, txtCartype };
        _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); LoadPartsAsync(); };
        LoadPartsAsync();
    }

    private async void LoadPartsAsync()
    {
        try
        {
            var partNo = string.IsNullOrWhiteSpace(txtPartNo.Text) ? null : txtPartNo.Text.Trim();
            var partName = string.IsNullOrWhiteSpace(txtPartName.Text) ? null : txtPartName.Text.Trim();
            var cartype = string.IsNullOrWhiteSpace(txtCartype.Text) ? null : txtCartype.Text.Trim();

            // 复用销售开单的拼音匹配逻辑（与 SellControl 一致）
            string? partNamePy = partName != null && !ContainsChinese(partName) ? PinyinHelper.GetPinyinInitials(partName) : null;
            string? cartypePy = cartype != null && !ContainsChinese(cartype) ? PinyinHelper.GetPinyinInitials(cartype) : null;

            List<PartData> results;
            if (string.IsNullOrEmpty(partNo) && string.IsNullOrEmpty(partName) && string.IsNullOrEmpty(cartype))
            {
                // 无条件时加载前200条
                var data = await _partRepo.GetStockListAsync(null, 200);
                results = data.Select(p => new PartData { Partid = p.PartId, Partno = p.PartNo, Name = p.Name,
                    Cartype = p.CarType, Carname = p.CarName, Unit = p.Unit, Place = p.Place,
                    Inprice = p.InPrice, Lsprice = p.LsPrice, Pfprice = p.PfPrice, Memo = p.Memo,
                    StockAmount = p.Amount, Isck = p.Isck, StockWarning = p.Warning }).ToList();
            }
            else
            {
                // 有条件时使用高级查询（含完整拼音匹配，包含模式）
                var data = await _partRepo.GetStockListAdvancedAsync(partNo, partName, partNamePy, cartype, cartypePy, null, null, 3);
                results = data.Select(p => new PartData { Partid = p.PartId, Partno = p.PartNo, Name = p.Name,
                    Cartype = p.CarType, Carname = p.CarName, Unit = p.Unit, Place = p.Place,
                    Inprice = p.InPrice, Lsprice = p.LsPrice, Pfprice = p.PfPrice, Memo = p.Memo,
                    StockAmount = p.Amount, Isck = p.Isck, StockWarning = p.Warning }).ToList();
            }

            _allParts = results;
            ApplyWasteFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载配件失败: {ex.Message}", "错误");
        }
    }

    private void ApplyWasteFilter()
    {
        var filtered = _hideWaste
            ? _allParts.Where(p => !string.Equals(p.Place, "废品仓", StringComparison.OrdinalIgnoreCase))
            : _allParts;
        Parts.Clear();
        foreach (var p in filtered)
            Parts.Add(p);
    }

    private static bool ContainsChinese(string text)
    {
        return text.Any(c => c >= 0x4e00 && c <= 0x9fff);
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void QueryTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox current) return;
        int index = Array.IndexOf(_queryTextBoxes, current);
        if (index < 0) return;

        if (e.Key == Key.Right)
        {
            e.Handled = true;
            int next = (index + 1) % 3;
            _queryTextBoxes[next].Focus();
            _queryTextBoxes[next].SelectAll();
        }
        else if (e.Key == Key.Left)
        {
            e.Handled = true;
            int prev = (index - 1 + 3) % 3;
            _queryTextBoxes[prev].Focus();
            _queryTextBoxes[prev].SelectAll();
        }
        else if (e.Key == Key.Down && dgParts.Items.Count > 0)
        {
            e.Handled = true;
            dgParts.SelectedIndex = 0;
            dgParts.ScrollIntoView(dgParts.Items[0]);
            Dispatcher.BeginInvoke(() => dgParts.Focus(), DispatcherPriority.Loaded);
        }
    }
    private void DgParts_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 预警列编辑中按回车：提交编辑，不触发其他操作
        if (e.Key == Key.Enter && dgParts.CurrentCell.Column?.Header as string == "预警")
        {
            e.Handled = true;
            dgParts.CommitEdit();
            return;
        }

        if (e.Key == Key.Enter && dgParts.SelectedItem != null)
        {
            e.Handled = true;
            DgParts_MouseDoubleClick(sender, null);
        }
        else if (e.Key == Key.F4 && dgParts.SelectedItem is PartData part)
        {
            e.Handled = true;
            OpenPartHistory(part);
        }
    }

    private void OpenPartHistory(PartData part)
    {
        var win = new PartHistoryWindow(_partQuery, part.Partid, part.Partno ?? "", part.Name);
        win.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        win.ShowDialog();
    }

    private void DgParts_MouseDoubleClick(object sender, MouseButtonEventArgs? e)
    {
        // 双击预警列时只编辑，不触发选择/采购窗口
        if (dgParts.CurrentCell.Column?.Header as string == "预警") return;

        if (dgParts.SelectedItem is not PartData part) return;

        if (PurchaseMode)
        {
            // 采购模式：弹出编辑窗口设置数量和金额
            OpenBuyEditDialog(part);
        }
        else if (BorrowMode)
        {
            // 借货模式：复用销售开单配件历史窗口(SellEditDialog)
            OpenBorrowEdit(part);
        }
        else
        {
            // 普通模式：直接选中并关闭
            SelectedParts.Clear();
            SelectedParts.Add(part);
            DialogResult = true;
            Close();
        }
    }

    /// <summary>
    /// 借货模式：复用销售开单配件历史窗口(SellEditDialog)，确认后通过 ItemAdded 实时加入借货明细。
    /// 窗口保持打开，可连续添加多个配件。
    /// </summary>
    private void OpenBorrowEdit(PartData part)
    {
        // 重复检测：已添加的配件提示是否继续
        var existingInResults = PurchaseResults.FirstOrDefault(r => r.PartId == part.Partid);
        var amountInDetails = ExistingPartAmounts.GetValueOrDefault(part.Partid, 0);
        var amountInResults = existingInResults?.Amount ?? 0;
        var totalAmount = amountInDetails + amountInResults;

        if (totalAmount > 0)
        {
            var msg = $"配件 {part.Partno} {part.Name} 已借出数量 {totalAmount}，是否继续添加？";
            if (MessageBox.Show(msg, "配件已添加", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
        }

        var defaultInPrice = part.Inprice == null ? 0m : Convert.ToDecimal(part.Inprice);
        var stockAmount = part.StockAmount ?? 0;

        // 复用销售开单配件历史窗口
        var dlg = new SellEditDialog(
            part.Partid, part.Partno ?? "", part.Name ?? "",
            part.Lsprice == null ? 0m : Convert.ToDecimal(part.Lsprice),
            part.Pfprice == null ? 0m : Convert.ToDecimal(part.Pfprice),
            stockAmount,
            App.ServiceProvider.GetRequiredService<ISellRepository>(),
            App.ServiceProvider.GetRequiredService<IBuyRepository>(),
            App.ServiceProvider.GetRequiredService<IClientRepository>(),
            App.ServiceProvider.GetRequiredService<IDbConnectionFactory>(),
            null,
            part.Cartype ?? "",
            readOnly: false)
        {
            Owner = this
        };

        if (dlg.ShowDialog() == true && dlg.IsConfirmed)
        {
            var resultAmount = dlg.Amount > 0 ? dlg.Amount : 1m;
            var resultPrice = dlg.Price;

            if (existingInResults != null)
            {
                existingInResults.Amount += resultAmount;
                existingInResults.InPrice = resultPrice;
                var incremental = new PartSelectResult
                {
                    PartId = part.Partid,
                    PartNo = part.Partno,
                    PartName = part.Name,
                    CarName = part.Carname,
                    Cartype = part.Cartype,
                    InPrice = resultPrice,
                    LsPrice = part.Lsprice == null ? 0m : Convert.ToDecimal(part.Lsprice),
                    PfPrice = part.Pfprice == null ? 0m : Convert.ToDecimal(part.Pfprice),
                    Amount = resultAmount,
                    Unit = part.Unit ?? ""
                };
                ItemAdded?.Invoke(incremental);
            }
            else
            {
                var result = new PartSelectResult
                {
                    PartId = part.Partid,
                    PartNo = part.Partno,
                    PartName = part.Name,
                    CarName = part.Carname,
                    Cartype = part.Cartype,
                    InPrice = resultPrice,
                    LsPrice = part.Lsprice == null ? 0m : Convert.ToDecimal(part.Lsprice),
                    PfPrice = part.Pfprice == null ? 0m : Convert.ToDecimal(part.Pfprice),
                    Amount = resultAmount,
                    Unit = part.Unit ?? ""
                };
                PurchaseResults.Add(result);
                ItemAdded?.Invoke(result);
            }
        }
    }

    private void OpenBuyEditDialog(PartData part)
    {
        // 计算配件总下单数量 = 已有明细数量 + 本次窗口内添加数量
        var existingInResults = PurchaseResults.FirstOrDefault(r => r.PartId == part.Partid);
        var amountInDetails = ExistingPartAmounts.GetValueOrDefault(part.Partid, 0);
        var amountInResults = existingInResults?.Amount ?? 0;
        var totalAmount = amountInDetails + amountInResults;
        var isExisting = totalAmount > 0;

        if (isExisting)
        {
            var msg = $"配件 {part.Partno} {part.Name} 已下单数量 {totalAmount}，是否增加下单数量？";
            if (MessageBox.Show(msg, "配件已下单", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
        }

        var defaultInPrice = part.Inprice == null ? 0m : Convert.ToDecimal(part.Inprice);

        var dlg = new PartQuantityDialog(
            part.Partno ?? "", part.Name ?? "", part.Cartype ?? "", part.Unit ?? "",
            defaultInPrice, 1);
        dlg.Owner = this;

        if (dlg.ShowDialog() == true)
        {
            if (existingInResults != null)
            {
                // 更新本地总数量（用于下次重复检测提示），回调只传增量
                existingInResults.Amount += dlg.ResultAmount;
                existingInResults.InPrice = dlg.ResultInPrice;
                var incremental = new PartSelectResult
                {
                    PartId = part.Partid,
                    PartNo = part.Partno,
                    PartName = part.Name,
                    CarName = part.Carname,
                    Cartype = part.Cartype,
                    InPrice = dlg.ResultInPrice,
                    LsPrice = part.Lsprice == null ? 0m : Convert.ToDecimal(part.Lsprice),
                    PfPrice = part.Pfprice == null ? 0m : Convert.ToDecimal(part.Pfprice),
                    Amount = dlg.ResultAmount,
                    Unit = part.Unit ?? ""
                };
                ItemAdded?.Invoke(incremental);
            }
            else
            {
                var result = new PartSelectResult
                {
                    PartId = part.Partid,
                    PartNo = part.Partno,
                    PartName = part.Name,
                    CarName = part.Carname,
                    Cartype = part.Cartype,
                    InPrice = dlg.ResultInPrice,
                    LsPrice = part.Lsprice == null ? 0m : Convert.ToDecimal(part.Lsprice),
                    PfPrice = part.Pfprice == null ? 0m : Convert.ToDecimal(part.Pfprice),
                    Amount = dlg.ResultAmount,
                    Unit = part.Unit ?? ""
                };
                PurchaseResults.Add(result);
                ItemAdded?.Invoke(result);
            }
        }
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (dgParts.SelectedItems.Count == 0)
        {
            MessageBox.Show("请选择配件", "提示");
            return;
        }

        if (PurchaseMode)
        {
            // 采购模式：对选中项逐个弹窗设置数量/金额
            foreach (PartData part in dgParts.SelectedItems)
            {
                OpenBuyEditDialog(part);
            }
        }
        else if (BorrowMode)
        {
            // 借货模式：对选中项逐个复用销售开单配件历史窗口
            foreach (PartData part in dgParts.SelectedItems)
            {
                OpenBorrowEdit(part);
            }
        }
        else
        {
            // 普通模式：全部选中关闭
            SelectedParts.Clear();
            foreach (PartData p in dgParts.SelectedItems) SelectedParts.Add(p);
            DialogResult = true;
            Close();
        }
    }

    private void DgParts_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
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

    private void MenuHideWaste_Click(object sender, RoutedEventArgs e)
    {
        var mi = (MenuItem)sender;
        _hideWaste = mi.IsChecked;
        ApplyWasteFilter();
    }

    private void DgParts_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        // 预警列进入编辑时自动全选
        if (e.Column.Header as string != "预警") return;
        if (e.EditingElement is TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private async void DgParts_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        // 仅处理预警列的编辑
        if (e.Column.Header as string != "预警") return;
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.Item is not PartData part) return;

        var textBox = e.EditingElement as TextBox;
        if (textBox == null) return;

        if (!long.TryParse(textBox.Text, out var newWarning)) return;

        try
        {
            await _partRepo.UpdateWarningAsync(part.Partid, newWarning);
            part.StockWarning = newWarning;
            // 直接更新当前行前景色
            _ = Dispatcher.BeginInvoke(() =>
            {
                var row = e.Row;
                if (row != null)
                    row.Foreground = part.IsLowStock ? Brushes.Red : Brushes.Black;
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"更新预警值失败: {ex.Message}", "错误");
        }
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
}
