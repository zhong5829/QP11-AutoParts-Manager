using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Interfaces;

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
                @"SELECT TOP 50
                  p.partno AS PartNo, p.name AS PartName,
                  SUM(ds.amount) AS TotalAmount, SUM(ds.stotal) AS TotalMoney
                  FROM detail_sell ds
                  INNER JOIN part_data p ON ds.partid = p.partid
                  INNER JOIN bill_sell bs ON ds.sn = bs.sn
                  WHERE bs.datetime >= @Start AND bs.datetime < DATEADD(day, 1, @End)
                  AND ISNULL(bs.flag, 0) <> -1
                  AND ds.stotal > 0
                  GROUP BY p.partno, p.name
                  ORDER BY TotalMoney DESC",
                new { Start = start, End = end })).ToList();
            for (int i = 0; i < partData.Count; i++) partData[i].Rank = i + 1;

            var clientData = (await db.QueryAsync<ClientRankItem>(
                @"SELECT TOP 50
                  c.name AS ClientName,
                  COUNT(*) AS OrderCount,
                  SUM(bs.total) AS TotalMoney,
                  ISNULL((SELECT SUM(total) FROM arrearage WHERE cid = bs.client), 0) AS Arrearage
                  FROM bill_sell bs
                  LEFT JOIN client_infor c ON bs.client = c.cid
                  WHERE bs.datetime >= @Start AND bs.datetime < DATEADD(day, 1, @End)
                  AND ISNULL(bs.flag, 0) <> -1
                  AND bs.total > 0
                  GROUP BY bs.client, c.cid, c.name
                  ORDER BY TotalMoney DESC",
                new { Start = start, End = end })).ToList();
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
}
