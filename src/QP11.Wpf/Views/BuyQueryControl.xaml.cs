using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QP11.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using QP11.Wpf.ViewModels;

namespace QP11.Wpf.Views;

public partial class BuyQueryControl : UserControl, ITabContent
{
    private readonly BuyQueryViewModel _viewModel;
    private readonly ISupplierRepository _supplierRepo = App.ServiceProvider.GetRequiredService<ISupplierRepository>();

    public string TabTitle => "采购单据查询";
    public bool HasUnsavedChanges => false;
    public event EventHandler? RequestClose;

    public BuyQueryControl(BuyQueryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        dtStart.SelectedDate = DateTime.Now.AddDays(-30);
        dtEnd.SelectedDate = DateTime.Now;
        Loaded += async (_, _) => await LoadSuppliersAsync();
        LoadBills();
    }

    private async System.Threading.Tasks.Task LoadSuppliersAsync()
    {
        try
        {
            var suppliers = (await _supplierRepo.GetAllAsync()).ToList();
            txtSupplier.SetSuppliers(suppliers);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载供应商列表失败");
        }
    }

    private async void LoadBills()
    {
        try
        {
            var data = (await _viewModel.LoadBillListAsync(
                dtStart.SelectedDate, dtEnd.SelectedDate,
                txtSupplier.SearchText.Trim(), txtWorker.Text.Trim())).ToList();

            dgBills.ItemsSource = data;

            txtCount.Text = $"单据数: {data.Count}";
            txtSumTotal.Text = data.Sum(r => (decimal)(r.total == null ? 0m : (decimal)r.total)).ToString("N2");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询失败: {ex.Message}", "错误");
        }
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => LoadBills();

    /// <summary>双击：备注列进入就地编辑（不弹窗），其他列打开明细窗口</summary>
    private void DgBills_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 双击前第一次点击必然已选中该单元格，用 CurrentColumn 判断是否备注列
        if (dgBills.CurrentColumn is not null && IsMemoColumn(dgBills.CurrentColumn)) return;

        // 双击前已选中其他列但本次双击落在备注格上的兜底判断
        var cell = FindDataGridCell(e.OriginalSource as System.Windows.DependencyObject);
        if (cell?.Column is not null && IsMemoColumn(cell.Column)) return;

        if (dgBills.SelectedItem == null) return;
        dynamic row = dgBills.SelectedItem;
        string? sn;
        try { sn = (string?)row.sn; } catch { return; }
        if (string.IsNullOrEmpty(sn)) return;
        var win = new BuyEditWindow(sn);
        win.BillMemoSaved += UpdateBillMemoInList;
        win.Owner = Window.GetWindow(this);
        win.Show();
    }

    /// <summary>备注列就地编辑提交后保存到 bill_buy.memo，未变化不写库</summary>
    private async void DgBills_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != System.Windows.Controls.DataGridEditAction.Commit) return;
        if (!IsMemoColumn(e.Column)) return;
        if (e.Row.Item is not System.Collections.Generic.IDictionary<string, object> dict) return;

        var newText = (e.EditingElement as System.Windows.Controls.TextBox)?.Text;
        var oldText = dict.TryGetValue("memo", out var v) ? v as string : null;
        if (newText == oldText) return;

        var sn = dict.TryGetValue("sn", out var sv) ? sv as string : null;
        if (string.IsNullOrEmpty(sn)) return;

        try
        {
            var repo = App.ServiceProvider.GetRequiredService<IBuyRepository>();
            await repo.UpdateBillMemoAsync(sn, newText);
            dict["memo"] = newText ?? "";
            // 编辑提交流程未结束前 Items 仍处于 EditItem 事务中（Refresh 会抛 InvalidOperationException），
            // 先提交退出事务再刷新；此时 dict 已写入新值，重复进入本事件会因新旧值相等而直接返回，不会死循环
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                dgBills.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
                dgBills.Items.Refresh();
            }));
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "保存采购单备注失败 sn={Sn}", sn);
            MessageBox.Show($"保存备注失败: {ex.Message}", "错误");
        }
    }

    /// <summary>明细窗口内保存单据头备注后，同步刷新列表对应行的备注列</summary>
    private void UpdateBillMemoInList(string sn, string? memo)
    {
        foreach (var item in dgBills.Items)
        {
            if (item is System.Collections.Generic.IDictionary<string, object> dict &&
                dict.TryGetValue("sn", out var sv) && (sv as string) == sn)
            {
                dict["memo"] = memo ?? "";
                dgBills.Items.Refresh();
                return;
            }
        }
    }

    private static bool IsMemoColumn(System.Windows.Controls.DataGridColumn column)
        => (column.Header as string) == "备注";

    private static System.Windows.Controls.DataGridCell? FindDataGridCell(System.Windows.DependencyObject? source)
    {
        while (source != null && source is not System.Windows.Controls.DataGridCell)
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        return source as System.Windows.Controls.DataGridCell;
    }

    private async void BtnVoid_Click(object sender, RoutedEventArgs e)
    {
        if (dgBills.SelectedItem == null) return;
        dynamic row = dgBills.SelectedItem;
        string? sn;
        try { sn = (string?)row.sn; } catch { return; }
        if (string.IsNullOrEmpty(sn)) return;
        if (MessageBox.Show($"确定删除单据 [{sn}]? 删除后不可恢复，库存将扣减", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            await _viewModel.VoidBillAsync(sn);
            LoadBills();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除失败: {ex.Message}", "错误");
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        dgBills.SelectAllCells();
        dgBills.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
        ApplicationCommands.Copy.Execute(null, dgBills);
        dgBills.UnselectAllCells();
        MessageBox.Show("数据已复制到剪贴板，可粘贴到Excel", "提示");
    }

    public void OnAdd() { }
    public void OnEdit() { }
    public void OnQuery() => LoadBills();
    public void OnDelete() { }
    public void OnSave() { }
    public void OnSettle() { }
    public void OnPrint() { }
    public void OnReturn() { }
    public void OnCancel() { }
    public void OnHistory() { }
    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);
}
