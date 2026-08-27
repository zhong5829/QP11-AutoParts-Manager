using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Models;
using QP11.Core.Interfaces;
using QP11.Services;

namespace QP11.Wpf.Views;

public partial class InventoryWindow : Window
{
    private readonly IPartRepository _partRepo;
    private readonly ExportService _exportService;
    private CancellationTokenSource? _searchCts;

    public InventoryWindow(IPartRepository partRepo, ExportService exportService)
    {
        _partRepo = partRepo;
        _exportService = exportService;
        InitializeComponent();
        LoadParts();
    }

    private async void LoadParts(string? keyword = null)
    {
        try
        {
            var data = (await _partRepo.GetStockListAsync(keyword)).ToList();
            dgParts.ItemsSource = data;
            txtCount.Text = $"共 {data.Count} 条记录";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载配件失败: {ex.Message}", "错误");
        }
    }

    private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        try
        {
            await Task.Delay(300, cts.Token);
        }
        catch (OperationCanceledException) { return; }

        if (cts.Token.IsCancellationRequested) return;

        var kw = txtSearch.Text.Trim();
        if (kw.Length >= 1) LoadParts(kw);
        else if (kw.Length == 0) LoadParts();
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => LoadParts(txtSearch.Text.Trim());

    private void BtnAlert_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var win = new StockAlertWindow(
            App.ServiceProvider.GetRequiredService<IPartRepository>(),
            App.ServiceProvider.GetRequiredService<IPartQueryService>()) { Owner = owner };
        win.ShowDialog();
    }

    private void BtnAddPart_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var dlg = new PartEditWindow { Owner = owner };
        if (dlg.ShowDialog() == true)
            LoadParts();
    }

    private void BtnEditPart_Click(object sender, RoutedEventArgs e)
    {
        if (dgParts.SelectedItem is not PartStockDisplay row) return;
        _ = EditPartAsync(row.PartId);
    }

    private async Task EditPartAsync(long partid)
    {
        try
        {
            var part = await _partRepo.GetByIdAsync(partid);
            if (part == null) return;
            var dlg = new PartEditWindow(part) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
                LoadParts();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"编辑失败: {ex.Message}", "错误");
        }
    }

    private void DgParts_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnEditPart_Click(sender, e);
    }

    private void DgParts_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is not PartStockDisplay row) return;
        try
        {
            var isck = row.Isck ?? 0L;
            e.Row.Foreground = isck > 0
                ? new SolidColorBrush(Colors.Blue)
                : new SolidColorBrush(Colors.Black);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "DgList_LoadingRow 失败");
        }
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (dgParts.ItemsSource is not System.Collections.Generic.List<PartStockDisplay> data || data.Count == 0)
        {
            MessageBox.Show("无数据可导出", "提示");
            return;
        }
        try
        {
            var exportItems = data.Select(i => new
            {
                配件编号 = i.PartNo,
                配件名称 = i.Name,
                出厂类别 = i.PartCclb,
                车型 = i.CarType,
                车系 = i.CarName,
                单位 = i.Unit,
                分类 = i.Class,
                区域品牌 = i.Area,
                库存数量 = i.Amount,
                批发价 = i.PfPrice,
                零售价 = i.LsPrice,
                规格 = i.PartGg,
                库位 = i.Place,
                图号 = i.PartTh,
                销售次数 = i.SellUse,
                保质期 = i.PartBzq,
                备注 = i.Memo
            }).ToList();
            var path = await _exportService.ExportToExcelAsync(exportItems, $"库存查询_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                "配件编号", "配件名称", "出厂类别", "车型", "车系", "单位", "分类", "区域品牌",
                "库存数量", "批发价", "零售价", "规格", "库位", "图号", "销售次数", "保质期", "备注");
            MessageBox.Show($"导出成功!\n文件: {path}", "提示");
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "导出库存数据失败"); MessageBox.Show($"导出失败: {ex.Message}", "错误"); }
    }
}
