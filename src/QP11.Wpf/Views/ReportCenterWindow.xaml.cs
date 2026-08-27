using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class ReportCenterWindow : Window
{
    public ReportCenterWindow()
    {
        InitializeComponent();
        dtStart.SelectedDate = DateTime.Now.AddDays(-30);
        dtEnd.SelectedDate = DateTime.Now;
    }

    private void LbReportType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
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
            IEnumerable<dynamic> data = tag switch
            {
                "SellSummary" => await db.QueryAsync(@"SELECT CONVERT(varchar, datetime, 23) AS 日期, COUNT(*) AS 单数, ISNULL(SUM(total),0) AS 金额 
                    FROM bill_sell WHERE ISNULL(flag,0) <> -1 AND datetime BETWEEN @Start AND @End GROUP BY CONVERT(varchar, datetime, 23) ORDER BY 日期", new { Start = start, End = end }),
                "BuySummary" => await db.QueryAsync(@"SELECT CONVERT(varchar, datetime, 23) AS 日期, COUNT(*) AS 单数, ISNULL(SUM(total),0) AS 金额 
                    FROM bill_buy WHERE ISNULL(flag,0) <> -1 AND datetime BETWEEN @Start AND @End GROUP BY CONVERT(varchar, datetime, 23) ORDER BY 日期", new { Start = start, End = end }),
                "SellRank" => await db.QueryAsync(@"SELECT TOP 20 p.name AS 配件名, p.partno AS 件号, SUM(d.amount) AS 总数量, SUM(d.stotal) AS 总金额 
                    FROM detail_sell d JOIN part_data p ON d.partid=p.partid JOIN bill_sell b ON d.sn=b.sn 
                    WHERE ISNULL(d.flag,0) <> -1 AND b.datetime BETWEEN @Start AND @End 
                    GROUP BY p.name, p.partno ORDER BY 总金额 DESC", new { Start = start, End = end }),
                "Inventory" => await db.QueryAsync(@"SELECT p.partno AS 件号, p.name AS 名称, p.carname AS 车型, p.unit AS 单位, ISNULL(s.amount,0) AS 库存, p.lsprice AS 零售价 
                    FROM part_data p LEFT JOIN part_stock s ON p.partid=s.partid WHERE (p.del IS NULL OR p.del<>'Y') ORDER BY p.partid"),
                "StockAlert" => await db.QueryAsync(@"SELECT p.partno AS 件号, p.name AS 名称, ISNULL(s.amount,0) AS 库存, p.lsprice AS 零售价 
                    FROM part_data p LEFT JOIN part_stock s ON p.partid=s.partid WHERE (p.del IS NULL OR p.del<>'Y') AND ISNULL(s.amount,0) <= 5 ORDER BY s.amount"),
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
