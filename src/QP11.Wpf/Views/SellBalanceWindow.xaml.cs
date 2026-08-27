using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

/// <summary>
/// 销售对账窗口，按客户和日期范围生成对账单
/// </summary>
public partial class SellBalanceWindow : Window
{
    private readonly ISellRepository _sellRepo;
    private readonly IClientRepository _clientRepo;
    public ObservableCollection<BillSell> Bills { get; } = new();

    public SellBalanceWindow(ISellRepository sellRepo, IClientRepository clientRepo)
    {
        _sellRepo = sellRepo;
        _clientRepo = clientRepo;
        InitializeComponent();
        dgBills.ItemsSource = Bills;
        dtStart.SelectedDate = DateTime.Now.AddDays(-90);
        dtEnd.SelectedDate = DateTime.Now;
        Loaded += async (_, _) => await LoadClientsAsync();
    }

    private async System.Threading.Tasks.Task LoadClientsAsync()
    {
        try
        {
            var clients = (await _clientRepo.GetAllAsync()).ToList();
            txtClient.SetClients(clients);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载客户列表失败");
        }
    }

    /// <summary>
    /// 生成对账单，汇总金额和欠款信息
    /// </summary>
    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        var client = txtClient.SelectedClientId ?? txtClient.SearchText.Trim();
        try
        {
            Bills.Clear();
            var data = await _sellRepo.GetListAsync(dtStart.SelectedDate, dtEnd.SelectedDate, client);
            foreach (var b in data) Bills.Add(b);

            var totalAmount = Bills.Sum(b => b.BillTotal ?? 0m);
            var totalPaid = Bills.Sum(b => (b.Cash ?? 0m) + (b.Weixin ?? 0m) + (b.Zhifubao ?? 0m));
            var arrear = totalAmount - totalPaid;

            txtCount.Text = $"共 {Bills.Count} 条记录";
            txtTotal.Text = $"总金额: {totalAmount:C2}";
            txtPaid.Text = $"已付: {totalPaid:C2}";
            txtArrear.Text = $"欠款: {arrear:C2}";
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "生成对账单失败"); MessageBox.Show($"生成对账单失败: {ex.Message}", "错误"); }
    }
}
