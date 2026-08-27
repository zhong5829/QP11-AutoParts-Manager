using System;
using System.Collections.ObjectModel;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

/// <summary>
/// 价格管理窗口，提供配件价格查看、编辑及批量调价功能
/// </summary>
public partial class PriceManagerWindow : Window
{
    private readonly IPartRepository _partRepo;
    public ObservableCollection<PartData> Parts { get; } = new();

    public PriceManagerWindow(IPartRepository partRepo)
    {
        _partRepo = partRepo;
        InitializeComponent();
        dgParts.ItemsSource = Parts;
        LoadParts();
    }

    /// <summary>
    /// 加载配件列表，支持关键词搜索
    /// </summary>
    private async void LoadParts(string? keyword = null)
    {
        try
        {
            Parts.Clear();
            var data = string.IsNullOrEmpty(keyword)
                ? await _partRepo.GetAllAsync()
                : await _partRepo.SearchAsync(keyword);
            foreach (var p in data) Parts.Add(p);
            txtCount.Text = $"共 {Parts.Count} 条记录";
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载配件价格失败"); MessageBox.Show($"加载失败: {ex.Message}", "错误"); }
    }

    /// <summary>
    /// 搜索框文本变化时自动查询（2个字符以上触发）
    /// </summary>
    private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var kw = txtSearch.Text.Trim();
        if (kw.Length >= 2) LoadParts(kw);
        else if (kw.Length == 0) LoadParts();
    }

    /// <summary>
    /// 查询按钮点击
    /// </summary>
    private void BtnSearch_Click(object sender, RoutedEventArgs e) => LoadParts(txtSearch.Text.Trim());

    /// <summary>
    /// 批量调价操作
    /// </summary>
    private async void BtnBatchAdjust_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(txtAdjustValue.Text, out var value)) { MessageBox.Show("请输入有效数值", "提示"); return; }
        var priceField = (cboPriceType.SelectedIndex) switch { 0 => "lsprice", 1 => "pfprice", _ => "inprice" };
        var count = dgParts.Items.Count;
        if (count == 0) return;

        if (MessageBox.Show($"确定对 {count} 条记录执行批量调价?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        try
        {
            foreach (PartData p in dgParts.Items)
            {
                decimal? oldPrice = priceField switch { "lsprice" => p.Lsprice, "pfprice" => p.Pfprice, _ => p.Inprice };
                decimal? newPrice;
                if (cboAdjustType.SelectedIndex == 0)
                    newPrice = Math.Round((oldPrice ?? 0m) * value, 2);
                else
                    newPrice = Math.Round((oldPrice ?? 0m) + value, 2);

                if (priceField == "lsprice") p.Lsprice = newPrice;
                else if (priceField == "pfprice") p.Pfprice = newPrice;
                else p.Inprice = newPrice;

                await _partRepo.UpdateAsync(p);
            }
            LoadParts();
            MessageBox.Show("批量调价完成!", "提示");
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "批量调价失败"); MessageBox.Show($"调价失败: {ex.Message}", "错误"); }
    }

    /// <summary>
    /// 保存当前选中配件的价格修改
    /// </summary>
    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (dgParts.SelectedItem is PartData part)
            {
                await _partRepo.UpdateAsync(part);
                MessageBox.Show("保存成功", "提示");
            }
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "保存配件价格失败"); MessageBox.Show($"保存失败: {ex.Message}", "错误"); }
    }
}
