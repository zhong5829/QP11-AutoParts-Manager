using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Interfaces;
using QP11.Services;

namespace QP11.Wpf.Views;

public class SupplierRatingItem
{
    public string? Sid { get; set; }
    public string? Name { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReturnRate { get; set; }
    public string? Rating { get; set; }
}

public partial class SupplierRatingWindow : Window
{
    public ObservableCollection<SupplierRatingItem> Items { get; } = new();
    private List<SupplierRatingItem> _allData = new();

    public SupplierRatingWindow()
    {
        InitializeComponent();
        dgList.ItemsSource = Items;
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var data = (await db.QueryAsync<SupplierRatingItem>(
                @"SELECT s.sid, s.name,
                  ISNULL((SELECT SUM(b.total) FROM bill_buy b WHERE b.supplier = s.sid AND ISNULL(b.flag, 0) <> -1 AND b.total > 0), 0) AS TotalAmount,
                  CASE WHEN ISNULL((SELECT COUNT(*) FROM bill_buy b WHERE b.supplier = s.sid AND ISNULL(b.flag, 0) <> -1 AND b.total > 0), 0) = 0 THEN 0
                       ELSE ISNULL((SELECT COUNT(*) FROM bill_buy b WHERE b.supplier = s.sid AND ISNULL(b.flag, 0) <> -1 AND b.total < 0), 0) * 100.0
                            / (SELECT COUNT(*) FROM bill_buy b WHERE b.supplier = s.sid AND ISNULL(b.flag, 0) <> -1 AND b.total > 0) END AS ReturnRate
                  FROM supplier_infor s
                  ORDER BY TotalAmount DESC")).ToList();

            foreach (var item in data)
            {
                item.Rating = item.ReturnRate switch
                {
                    < 2 => "A",
                    < 5 => "B",
                    < 10 => "C",
                    _ => "D"
                };
            }

            _allData = data;
            ApplyFilter();
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "查询供应商评级失败"); MessageBox.Show($"查询失败: {ex.Message}", "错误"); }
    }

    private void CboRating_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var selected = (cboRating.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var filtered = selected == "全部"
            ? _allData
            : _allData.Where(i => i.Rating == selected).ToList();

        Items.Clear();
        foreach (var item in filtered) Items.Add(item);
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (Items.Count == 0) { MessageBox.Show("没有数据可导出", "提示"); return; }
        try
        {
            var exportService = new ExportService();
            var path = await exportService.ExportToExcelAsync(Items,
                $"供应商评级_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                "Sid", "Name", "TotalAmount", "ReturnRate", "Rating");
            MessageBox.Show($"导出成功!\n文件: {path}", "提示");
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "导出供应商评级失败"); MessageBox.Show($"导出失败: {ex.Message}", "错误"); }
    }
}
