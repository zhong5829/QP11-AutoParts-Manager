using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace QP11.Wpf.Views;

/// <summary>
/// 销售退货明细项
/// </summary>
public class SellReturnDetailItem
{
    public bool IsReturn { get; set; }
    public long? Partid { get; set; }
    public string? PartName { get; set; }
    public decimal OrigAmount { get; set; }
    public decimal ReturnAmount { get; set; }
    public decimal Price { get; set; }
    public decimal ReturnTotal => Math.Round(Price * ReturnAmount, 2);
}

/// <summary>
/// 销售退货窗口，根据原销售单进行退货操作
/// </summary>
public partial class SellReturnWindow : Window
{
    private readonly ISellRepository _sellRepo;
    private readonly IPartRepository _partRepo;
    public ObservableCollection<SellReturnDetailItem> Details { get; } = new();

    public SellReturnWindow(ISellRepository sellRepo, IPartRepository partRepo)
    {
        _sellRepo = sellRepo;
        _partRepo = partRepo;
        InitializeComponent();
        dgDetails.ItemsSource = Details;
        dtReturnDate.SelectedDate = DateTime.Now;
    }

    /// <summary>
    /// 根据原单号加载销售单明细
    /// </summary>
    private async void BtnLoadOrig_Click(object sender, RoutedEventArgs e)
    {
        var sn = txtOrigSn.Text.Trim();
        if (string.IsNullOrEmpty(sn))
        {
            MessageBox.Show("请输入原销售单号", "提示");
            return;
        }

        try
        {
            var bill = await _sellRepo.GetBySnAsync(sn);
            if (bill == null || bill.Flag == (int)BusinessConstants.BillFlag.Voided)
            {
                MessageBox.Show("未找到该销售单", "提示");
                return;
            }

            var details = await _sellRepo.GetDetailsAsync(sn);
            Details.Clear();
            foreach (var d in details)
            {
                var part = d.Partid.HasValue ? await _partRepo.GetByIdAsync(d.Partid.Value) : null;
                Details.Add(new SellReturnDetailItem
                {
                    IsReturn = true,
                    Partid = d.Partid,
                    PartName = part?.Name ?? "",
                    OrigAmount = d.Amount ?? 0,
                    ReturnAmount = d.Amount ?? 0,
                    Price = d.Price ?? 0
                });
            }
            UpdateTotal();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询原单失败: {ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 更新退货合计金额
    /// </summary>
    private void UpdateTotal()
    {
        var total = Details.Where(d => d.IsReturn).Sum(d => d.ReturnTotal);
        txtTotalAmount.Text = total.ToString("C2");
    }

    /// <summary>
    /// 确认退货，生成退货单并回滚库存
    /// </summary>
    private async void BtnConfirmReturn_Click(object sender, RoutedEventArgs e)
    {
        var returnItems = Details.Where(d => d.IsReturn && d.ReturnAmount > 0).ToList();
        if (returnItems.Count == 0)
        {
            MessageBox.Show("请选择要退货的配件", "提示");
            return;
        }

        if (MessageBox.Show($"确认退货 {returnItems.Count} 项，合计 {returnItems.Sum(d => d.ReturnTotal):C2}?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        try
        {
            var snService = App.ServiceProvider.GetRequiredService<ISerialNumberService>();
            var returnSn = await snService.GenerateSellReturnSN();
            txtReturnSn.Text = returnSn;

            var bill = new BillSell
            {
                Sn = returnSn,
                Client = txtOrigSn.Text.Trim(),
                Total = -returnItems.Sum(d => d.ReturnTotal),
                BillTotal = -returnItems.Sum(d => d.ReturnTotal),
                DiscountRate = 1,
                Flag = 2,
                Memo = $"退货-原单:{txtOrigSn.Text.Trim()}"
            };
            await _sellRepo.InsertBillAsync(bill);

            foreach (var item in returnItems)
            {
                await _sellRepo.InsertDetailAsync(new DetailSell
                {
                    Sn = returnSn,
                    Partid = item.Partid,
                    Amount = -(long)item.ReturnAmount,
                    Price = item.Price,
                    BillPrice = item.Price,
                    Stotal = -item.ReturnTotal,
                    Flag = (int)BusinessConstants.BillFlag.Returned,
                    DiscountRate = 1
                });

                if (item.Partid.HasValue)
                    await _partRepo.IncreaseStockAsync(item.Partid.Value, item.ReturnAmount);
            }

            MessageBox.Show($"退货成功!\n退货单号: {returnSn}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"退货失败: {ex.Message}", "错误");
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
