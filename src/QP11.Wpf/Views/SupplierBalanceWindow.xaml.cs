using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public class SupplierBalanceRecord
{
    public DateTime? Datetime { get; set; }
    public string? Sn { get; set; }
    public string? TypeName { get; set; }
    public decimal BuyAmount { get; set; }
    public decimal ReturnAmount { get; set; }
    public decimal PayAmount { get; set; }
    public decimal Balance { get; set; }
}

public partial class SupplierBalanceWindow : Window
{
    private string? _supplierId;
    public ObservableCollection<SupplierBalanceRecord> Records { get; } = new();

    public SupplierBalanceWindow()
    {
        InitializeComponent();
        dgRecords.ItemsSource = Records;
        dtStart.SelectedDate = DateTime.Now.AddMonths(-1);
        dtEnd.SelectedDate = DateTime.Now;
    }

    private void BtnSelectSupplier_Click(object sender, RoutedEventArgs e)
    {
        var selector = new SupplierSelectorWindow { Owner = Window.GetWindow(this) };
        if (selector.ShowDialog() == true && selector.SelectedSupplier != null)
        {
            _supplierId = selector.SelectedSupplier.Sid;
            txtSupplier.Text = selector.SelectedSupplier.Name;
        }
    }

    private async void BtnQuery_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_supplierId))
        {
            MessageBox.Show("请选择供应商", "提示");
            return;
        }

        var start = dtStart.SelectedDate ?? DateTime.Now.AddMonths(-1);
        var end = dtEnd.SelectedDate ?? DateTime.Now;

        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();

            var buyRecords = (await db.QueryAsync<dynamic>(
                @"SELECT sn, datetime, total FROM bill_buy
                  WHERE supplier = @Sid AND datetime >= @Start AND datetime < DATEADD(day, 1, @End)
                  AND ISNULL(flag, 0) <> -1 AND total > 0
                  ORDER BY datetime",
                new { Sid = _supplierId, Start = start, End = end })).ToList();

            var returnRecords = (await db.QueryAsync<dynamic>(
                @"SELECT sn, datetime, ABS(total) as total FROM bill_buy
                  WHERE supplier = @Sid AND datetime >= @Start AND datetime < DATEADD(day, 1, @End)
                  AND ISNULL(flag, 0) <> -1 AND total < 0
                  ORDER BY datetime",
                new { Sid = _supplierId, Start = start, End = end })).ToList();

            var payRecords = (await db.QueryAsync<dynamic>(
                @"SELECT sn, datetime, je as total FROM pays
                  WHERE cid = @Sid AND datetime >= @Start AND datetime < DATEADD(day, 1, @End)
                  ORDER BY datetime",
                new { Sid = _supplierId, Start = start, End = end })).ToList();

            Records.Clear();
            decimal runningBalance = 0;

            var allRecords = buyRecords.Select(r => new { Date = (DateTime?)r.datetime, Sn = (string?)r.sn, Type = "采购", BuyAmt = (decimal)r.total, RetAmt = 0m, PayAmt = 0m })
                .Concat(returnRecords.Select(r => new { Date = (DateTime?)r.datetime, Sn = (string?)r.sn, Type = "退货", BuyAmt = 0m, RetAmt = (decimal)r.total, PayAmt = 0m }))
                .Concat(payRecords.Select(r => new { Date = (DateTime?)r.datetime, Sn = (string?)r.sn, Type = "付款", BuyAmt = 0m, RetAmt = 0m, PayAmt = (decimal)r.total }))
                .OrderBy(r => r.Date).ToList();

            foreach (var r in allRecords)
            {
                runningBalance += r.BuyAmt - r.RetAmt - r.PayAmt;
                Records.Add(new SupplierBalanceRecord
                {
                    Datetime = r.Date,
                    Sn = r.Sn,
                    TypeName = r.Type,
                    BuyAmount = r.BuyAmt,
                    ReturnAmount = r.RetAmt,
                    PayAmount = r.PayAmt,
                    Balance = runningBalance
                });
            }

            txtTotalBalance.Text = runningBalance.ToString("C2");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询失败: {ex.Message}", "错误");
        }
    }
}
