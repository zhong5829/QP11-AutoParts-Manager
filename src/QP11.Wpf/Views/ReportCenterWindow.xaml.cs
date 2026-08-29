using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Interfaces;
using QP11.Services;

namespace QP11.Wpf.Views;

public partial class ReportCenterWindow : Window
{
    public ReportCenterWindow()
    {
        InitializeComponent();
        dtStart.SelectedDate = DateTime.Now.AddDays(-30);
        dtEnd.SelectedDate = DateTime.Now;
        // 默认选中第一项（触发 SelectionChanged，但因未 Loaded 会被跳过，由 Window_Loaded 触发首次生成）
        lbReportType.SelectedIndex = 0;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await GenerateReportAsync();
    }

    private async void LbReportType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return; // 构造阶段 InitializeComponent 时也会触发一次，避免提前查询
        await GenerateReportAsync();
    }

    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        await GenerateReportAsync();
    }

    /// <summary>生成当前选中报表类型的数据并填充 DataGrid</summary>
    private async System.Threading.Tasks.Task GenerateReportAsync()
    {
        if (lbReportType.SelectedItem is not ListBoxItem item) return;
        var tag = item.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var start = dtStart.SelectedDate;
            var end = dtEnd.SelectedDate;
            if (start == null || end == null)
            {
                MessageBox.Show("请先选择起始和截止日期", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IEnumerable<dynamic> data = tag switch
            {
                "SellSummary" => await db.QueryAsync(@"SELECT CONVERT(varchar, datetime, 23) AS 日期, COUNT(*) AS 单数, ISNULL(SUM(total),0) AS 金额 
                    FROM bill_sell WHERE ISNULL(flag,0) <> -1 AND datetime >= @Start AND datetime < DATEADD(day, 1, @End) GROUP BY CONVERT(varchar, datetime, 23) ORDER BY 日期", new { Start = start, End = end }),
                "BuySummary" => await db.QueryAsync(@"SELECT CONVERT(varchar, datetime, 23) AS 日期, COUNT(*) AS 单数, ISNULL(SUM(total),0) AS 金额 
                    FROM bill_buy WHERE ISNULL(flag,0) <> -1 AND datetime >= @Start AND datetime < DATEADD(day, 1, @End) GROUP BY CONVERT(varchar, datetime, 23) ORDER BY 日期", new { Start = start, End = end }),
                "SellRank" => await db.QueryAsync(@"SELECT p.name AS 配件名, p.partno AS 件号, SUM(d.amount) AS 总数量, SUM(d.stotal) AS 总金额 
                    FROM detail_sell d JOIN part_data p ON d.partid=p.partid JOIN bill_sell b ON d.sn=b.sn 
                    WHERE ISNULL(d.flag,0) <> -1 AND b.datetime >= @Start AND b.datetime < DATEADD(day, 1, @End) 
                    GROUP BY p.name, p.partno ORDER BY 总数量 DESC", new { Start = start, End = end }),
                "SellDetail" => await db.QueryAsync(@"SELECT b.sn AS 单号, CONVERT(varchar, b.datetime, 23) AS 日期, ISNULL(c.name,'') AS 客户, d.partno AS 件号, d.name AS 名称, 
                    d.amount AS 数量, d.unit AS 单位, d.price AS 单价, d.stotal AS 金额,
                    ISNULL(d.cb, 0) * ISNULL(d.amount, 0) AS 成本,
                    ISNULL(d.stotal, 0) - ISNULL(d.cb, 0) * ISNULL(d.amount, 0) AS 利润
                    FROM detail_sell d
                    LEFT JOIN bill_sell b ON b.sn = d.sn
                    LEFT JOIN client_infor c ON c.cid = b.client
                    WHERE ISNULL(b.flag,0) <> -1 AND d.amount <> 0 AND b.datetime >= @Start AND b.datetime < DATEADD(day, 1, @End)
                    ORDER BY b.datetime, b.sn", new { Start = start, End = end }),
                "BuyDetail" => await db.QueryAsync(@"SELECT d.sn AS 单号, CONVERT(varchar, d.datetime, 23) AS 日期, ISNULL(s.name,'') AS 供应商, d.partno AS 件号, d.name AS 名称, 
                    d.amount AS 数量, d.unit AS 单位, d.inprice AS 进价, d.intotal AS 金额
                    FROM detail_buy d
                    LEFT JOIN bill_buy b ON b.sn = d.sn
                    LEFT JOIN supplier_infor s ON s.sid = b.supplier
                    WHERE ISNULL(b.flag,0) <> -1 AND d.datetime >= @Start AND d.datetime < DATEADD(day, 1, @End)
                    ORDER BY d.datetime, d.sn", new { Start = start, End = end }),
                "Inventory" => await db.QueryAsync(@"SELECT p.partno AS 件号, p.name AS 名称, p.carname AS 车型, p.unit AS 单位, ISNULL(s.amount,0) AS 库存, p.lsprice AS 零售价 
                    FROM part_data p LEFT JOIN part_stock s ON p.partid=s.partid WHERE (p.del IS NULL OR p.del<>'Y') ORDER BY p.partid"),
                "StockAlert" => await db.QueryAsync(@"SELECT p.partno AS 件号, p.name AS 名称, ISNULL(s.amount,0) AS 库存, p.lsprice AS 零售价 
                    FROM part_data p LEFT JOIN part_stock s ON p.partid=s.partid WHERE (p.del IS NULL OR p.del<>'Y') AND ISNULL(s.amount,0) <= 5 ORDER BY s.amount"),
                "Arrearage" => await db.QueryAsync(@"-- 应付（供应商 type=1）：bill_sell 关联恒不匹配，无需 join，直接 total-charge 汇总
                    SELECT '应付' AS 类别, supplier_infor.name AS 名称,
                           ISNULL(SUM(arrearage.total - ISNULL(arrearage.charge, 0)), 0) AS 欠款
                    FROM supplier_infor
                    INNER JOIN arrearage ON arrearage.bid = supplier_infor.sid AND arrearage.type = 1
                    GROUP BY supplier_infor.name
                    UNION ALL
                    -- 应收（客户 type=2）：需关联 bill_sell 判断退货单（flag=2 或 total<0 取反）
                    SELECT '应收', client_infor.name,
                           ISNULL(SUM(CASE WHEN arrearage.total < 0 THEN arrearage.total - ISNULL(arrearage.charge,0)
                               WHEN bs.flag = 2 OR bs.total < 0 THEN -(arrearage.total - ISNULL(arrearage.charge,0))
                               ELSE arrearage.total - ISNULL(arrearage.charge,0) END), 0)
                    FROM client_infor
                    INNER JOIN arrearage ON arrearage.bid = client_infor.cid AND arrearage.type = 2
                    LEFT JOIN bill_sell bs ON arrearage.sn = bs.sn
                    GROUP BY client_infor.name
                    ORDER BY 类别", new { }),
                _ => new List<dynamic>()
            };

            var table = ToDataTable(data);
            dgReport.ItemsSource = table.DefaultView;
            txtReportInfo.Text = $"共 {table.Rows.Count} 条记录 | 报表: {item.Content}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"生成报表失败: {ex.Message}", "错误");
        }
    }

    /// <summary>导出当前报表到 Excel，文件名含报表类型与日期范围</summary>
    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (lbReportType.SelectedItem is not ListBoxItem item) return;
        if (dgReport.ItemsSource is not DataView view || view.Count == 0 || view.Table == null)
        {
            MessageBox.Show("没有可导出的数据，请先生成报表", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var exportSvc = App.ServiceProvider.GetRequiredService<ExportService>();
            var start = dtStart.SelectedDate ?? DateTime.Now.AddDays(-30);
            var end = dtEnd.SelectedDate ?? DateTime.Now;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出报表",
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = $"{item.Content}_{start:yyyyMMdd}-{end:yyyyMMdd}.xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            if (dlg.ShowDialog() != true) return;
            var (path, error) = await exportSvc.ExportMultiSheetToPathAsync(
                dlg.FileName, (view.Table, item.Content.ToString()!, new HashSet<int>()));
            if (error != null) MessageBox.Show(error, "导出失败");
            else MessageBox.Show($"导出成功：{path}", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败: {ex.Message}", "错误");
        }
    }

    private static DataTable ToDataTable(IEnumerable<dynamic> data)
    {
        var table = new DataTable();
        using var reader = data.AsList().GetEnumerator();
        if (!reader.MoveNext() || reader.Current == null) return table;
        var first = (IDictionary<string, object>)reader.Current!;
        foreach (var key in first.Keys) table.Columns.Add(key);
        AddRow(table, first);
        while (reader.MoveNext()) AddRow(table, (IDictionary<string, object>)reader.Current!);
        return table;
    }

    private static void AddRow(DataTable table, IDictionary<string, object> dict)
    {
        var row = table.NewRow();
        foreach (var kv in dict) row[kv.Key] = kv.Value ?? DBNull.Value;
        table.Rows.Add(row);
    }
}
