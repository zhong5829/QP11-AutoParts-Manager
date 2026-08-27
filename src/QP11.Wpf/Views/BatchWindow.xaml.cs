using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class BatchWindow : Window
{
    private readonly IPartBatchRepository _repo;
    public ObservableCollection<PartBatch> Items { get; } = new();

    public BatchWindow(IPartBatchRepository repo)
    {
        _repo = repo;
        InitializeComponent();
        dgList.ItemsSource = Items;
    }

    private async void BtnQuery_Click(object sender, RoutedEventArgs e)
    {
        if (!long.TryParse(txtPartId.Text.Trim(), out var partid))
        {
            MessageBox.Show("请输入有效的配件ID", "提示");
            return;
        }

        try
        {
            var data = await _repo.GetByPartIdAsync(partid);
            Items.Clear();
            foreach (var item in data) Items.Add(item);
            txtCount.Text = $"共 {Items.Count} 条批次记录";
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "查询批次失败"); MessageBox.Show($"查询失败: {ex.Message}", "错误"); }
    }

    private async void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (!long.TryParse(txtPartId.Text.Trim(), out var partid))
        {
            MessageBox.Show("请先输入有效的配件ID", "提示");
            return;
        }

        var dlg = new InputBoxDialog("新增批次", "数量,仓位", "") { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
        {
            var parts = dlg.InputText?.Split(',') ?? Array.Empty<string>();
            try
            {
                var amount = parts.Length > 0 && decimal.TryParse(parts[0].Trim(), out var a) ? a : 0;
                var place = parts.Length > 1 ? parts[1].Trim() : "";
                await _repo.InsertAsync(new PartBatch
                {
                    Partid = partid,
                    Amount = amount,
                    Remain = amount,
                    Memo = place
                });
                BtnQuery_Click(sender, e);
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "新增批次失败"); MessageBox.Show($"新增失败: {ex.Message}", "错误"); }
        }
    }

    private async void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (dgList.SelectedItem is not PartBatch item) { MessageBox.Show("请选择批次记录", "提示"); return; }
        var dlg = new InputBoxDialog("编辑数量", "剩余数量", item.Remain?.ToString() ?? "0") { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
        {
            if (decimal.TryParse(dlg.InputText?.Trim(), out var remain))
            {
                try { await _repo.UpdateRemainAsync(item.Id, remain); BtnQuery_Click(sender, e); }
                catch (Exception ex) { Serilog.Log.Warning(ex, "编辑批次失败"); MessageBox.Show($"编辑失败: {ex.Message}", "错误"); }
            }
            else { MessageBox.Show("请输入有效数量", "提示"); }
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (dgList.SelectedItem is not PartBatch item) { MessageBox.Show("请选择批次记录", "提示"); return; }
        if (MessageBox.Show("确认删除该批次?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { await _repo.LogicDeleteAsync(item.Id); BtnQuery_Click(sender, e); }
        catch (Exception ex) { Serilog.Log.Warning(ex, "删除批次失败"); MessageBox.Show($"删除失败: {ex.Message}", "错误"); }
    }

    private async void BtnExpiring_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var data = await _repo.GetExpiringAsync(30);
            Items.Clear();
            foreach (var item in data) Items.Add(item);
            txtCount.Text = $"近30天过期: {Items.Count} 条";
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "查询临期批次失败"); MessageBox.Show($"查询失败: {ex.Message}", "错误"); }
    }
}
