using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Interfaces;
using QP11.Services;

namespace QP11.Wpf.Views;

public class PartRankItem
{
    public int Rank { get; set; }
    public string? PartNo { get; set; }
    public string? PartName { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalMoney { get; set; }
}

public class ClientRankItem
{
    public int Rank { get; set; }
    public string? ClientName { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalMoney { get; set; }
    public decimal Arrearage { get; set; }

    /// <summary>净订单数为负（退货单多于销售单）时用于红色字体高亮</summary>
    public bool IsNegativeOrder => OrderCount < 0;
}

public class WorkerRankItem
{
    public int Rank { get; set; }
    public string? WorkerName { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalMoney { get; set; }
}

public partial class RankingWindow : Window
{
    public ObservableCollection<PartRankItem> PartRanks { get; } = new();
    public ObservableCollection<ClientRankItem> ClientRanks { get; } = new();
    public ObservableCollection<WorkerRankItem> WorkerRanks { get; } = new();

    public RankingWindow()
    {
        InitializeComponent();
        dgPartRank.ItemsSource = PartRanks;
        dgClientRank.ItemsSource = ClientRanks;
        dgWorkerRank.ItemsSource = WorkerRanks;
        dtStart.SelectedDate = DateTime.Now.AddMonths(-1);
        dtEnd.SelectedDate = DateTime.Now;
    }

    private async void BtnQuery_Click(object sender, RoutedEventArgs e)
    {
        var start = dtStart.SelectedDate ?? DateTime.Now.AddMonths(-1);
        var end = dtEnd.SelectedDate ?? DateTime.Now;

        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();

            var partData = (await db.QueryAsync<PartRankItem>(
                @"SELECT p.partno AS PartNo, p.name AS PartName,
                  SUM(ds.amount) AS TotalAmount, SUM(ds.stotal) AS TotalMoney
                  FROM detail_sell ds
                  INNER JOIN part_data p ON ds.partid = p.partid
                  INNER JOIN bill_sell bs ON ds.sn = bs.sn
                  WHERE bs.datetime >= @Start AND bs.datetime < DATEADD(day, 1, @End)
                  AND ISNULL(bs.flag, 0) <> -1
                  AND ds.stotal > 0
                  GROUP BY p.partno, p.name
                  ORDER BY TotalAmount DESC",
                new { Start = start, End = end })).ToList();
            // 客户端按销售数量降序兜底排序（确保展示顺序为数量的从大到小，不依赖 SQL/DataGrid 排序行为）
            partData = partData.OrderByDescending(x => x.TotalAmount).ToList();
            for (int i = 0; i < partData.Count; i++) partData[i].Rank = i + 1;

            // 客户排行：显示全部客户（不再限定 TOP 50）
            // 性能优化：欠款不再用按行相关子查询（原 cid 列实际不存在于 arrearage 表），
            // 改为一次预聚合 + LEFT JOIN；欠款口径 = SUM(total - charge)，与 ArrearageRepository 统计一致
            var clientData = (await db.QueryAsync<ClientRankItem>(
                @"SELECT c.name AS ClientName,
                  b.OrderCount,
                  b.TotalMoney,
                  ISNULL(ag.Arrearage, 0) AS Arrearage
                  FROM (
                      SELECT bs.client,
                             SUM(CASE WHEN bs.flag = 2 OR bs.total < 0 THEN -1 ELSE 1 END) AS OrderCount,
                             SUM(bs.total) AS TotalMoney
                      FROM bill_sell bs
                      WHERE bs.datetime >= @Start AND bs.datetime < DATEADD(day, 1, @End)
                      AND ISNULL(bs.flag, 0) <> -1
                      GROUP BY bs.client
                  ) b
                  LEFT JOIN client_infor c ON b.client = c.cid
                  LEFT JOIN (
                      SELECT a.bid, SUM(ISNULL(a.total, 0) - ISNULL(a.charge, 0)) AS Arrearage
                      FROM arrearage a
                      GROUP BY a.bid
                  ) ag ON ag.bid = b.client
                  ORDER BY b.OrderCount DESC",
                new { Start = start, End = end })).ToList();
            // 客户端按订单数降序兜底排序（确保展示顺序为订单数的从大到小，不依赖 SQL/DataGrid 排序行为）
            clientData = clientData.OrderByDescending(x => x.OrderCount).ToList();
            for (int i = 0; i < clientData.Count; i++) clientData[i].Rank = i + 1;

            var workerData = (await db.QueryAsync<WorkerRankItem>(
                @"SELECT TOP 50
                  ISNULL(wi.name, bs.worker) AS WorkerName,
                  COUNT(*) AS OrderCount,
                  SUM(bs.total) AS TotalMoney
                  FROM bill_sell bs
                  LEFT JOIN work_infor wi ON bs.worker = wi.workid
                  WHERE bs.datetime >= @Start AND bs.datetime < DATEADD(day, 1, @End)
                  AND ISNULL(bs.flag, 0) <> -1
                  AND bs.total > 0
                  GROUP BY bs.worker, wi.name
                  ORDER BY TotalMoney DESC",
                new { Start = start, End = end })).ToList();
            for (int i = 0; i < workerData.Count; i++) workerData[i].Rank = i + 1;

            PartRanks.Clear();
            foreach (var item in partData) PartRanks.Add(item);

            ClientRanks.Clear();
            foreach (var item in clientData) ClientRanks.Add(item);

            WorkerRanks.Clear();
            foreach (var item in workerData) WorkerRanks.Add(item);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询失败: {ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 导出当前激活 Tab 的排行数据到 Excel（客户排行保留红色行标记）
    /// </summary>
    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var start = dtStart.SelectedDate ?? DateTime.Now.AddMonths(-1);
            var end = dtEnd.SelectedDate ?? DateTime.Now;

            if (tcMain.SelectedIndex == 0)
            {
                // 销售排行（配件）
                if (PartRanks.Count == 0) { MessageBox.Show("无数据可导出，请先查询", "提示"); return; }
                var dt = new DataTable();
                dt.Columns.Add("排名");
                dt.Columns.Add("配件编码");
                dt.Columns.Add("配件名称");
                dt.Columns.Add("销售数量");
                dt.Columns.Add("销售金额");
                foreach (var item in PartRanks)
                {
                    var row = dt.NewRow();
                    row["排名"] = item.Rank;
                    row["配件编码"] = item.PartNo ?? "";
                    row["配件名称"] = item.PartName ?? "";
                    row["销售数量"] = item.TotalAmount;
                    row["销售金额"] = item.TotalMoney;
                    dt.Rows.Add(row);
                }
                var exportSvc = App.ServiceProvider.GetRequiredService<ExportService>();
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "导出销售排行",
                    Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                    FileName = $"销售排行_{start:yyyyMMdd}-{end:yyyyMMdd}.xlsx",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };
                if (dlg.ShowDialog() != true) return;
                var (path, error) = await exportSvc.ExportMultiSheetToPathAsync(
                    dlg.FileName, (dt, "销售排行", new HashSet<int>()));
                if (error != null) MessageBox.Show(error, "导出失败");
                else MessageBox.Show($"导出成功：{path}", "提示");
            }
            else if (tcMain.SelectedIndex == 1)
            {
                // TOP客户（净订单数为负的行导出时标红，与界面一致）
                if (ClientRanks.Count == 0) { MessageBox.Show("无数据可导出，请先查询", "提示"); return; }
                var dt = new DataTable();
                dt.Columns.Add("排名");
                dt.Columns.Add("客户名称");
                dt.Columns.Add("订单数");
                dt.Columns.Add("销售金额");
                dt.Columns.Add("欠款");
                var redRows = new HashSet<int>();
                for (int i = 0; i < ClientRanks.Count; i++)
                {
                    var item = ClientRanks[i];
                    if (item.IsNegativeOrder) redRows.Add(i);
                    var row = dt.NewRow();
                    row["排名"] = item.Rank;
                    row["客户名称"] = item.ClientName ?? "";
                    row["订单数"] = item.OrderCount;
                    row["销售金额"] = item.TotalMoney;
                    row["欠款"] = item.Arrearage;
                    dt.Rows.Add(row);
                }
                var exportSvc = App.ServiceProvider.GetRequiredService<ExportService>();
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "导出TOP客户",
                    Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                    FileName = $"客户排行_{start:yyyyMMdd}-{end:yyyyMMdd}.xlsx",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };
                if (dlg.ShowDialog() != true) return;
                var (path, error) = await exportSvc.ExportMultiSheetToPathAsync(
                    dlg.FileName, (dt, "TOP客户", redRows));
                if (error != null) MessageBox.Show(error, "导出失败");
                else MessageBox.Show($"导出成功：{path}", "提示");
            }
            else
            {
                MessageBox.Show("仅支持导出销售排行和TOP客户", "提示");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败: {ex.Message}", "错误");
        }
    }
}
