using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace QP11.Wpf.Views;

public class ExchangeReturnItem
{
    public bool IsReturn { get; set; }
    public long? Partid { get; set; }
    public string? PartName { get; set; }
    public decimal OrigAmount { get; set; }
    public decimal ReturnAmount { get; set; }
    public decimal Price { get; set; }
    public decimal ReturnTotal => Math.Round(Price * ReturnAmount, 2);
}

public class ExchangeNewItem
{
    public long? Partid { get; set; }
    public string? PartName { get; set; }
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public decimal LineTotal => Math.Round(Price * Amount, 2);
}

public partial class SellExchangeWindow : Window
{
    private readonly ISellRepository _sellRepo;
    private readonly IPartRepository _partRepo;
    public ObservableCollection<ExchangeReturnItem> ReturnItems { get; } = new();
    public ObservableCollection<ExchangeNewItem> NewItems { get; } = new();

    public SellExchangeWindow(ISellRepository sellRepo, IPartRepository partRepo)
    {
        _sellRepo = sellRepo;
        _partRepo = partRepo;
        InitializeComponent();
        dgReturnItems.ItemsSource = ReturnItems;
        dgNewItems.ItemsSource = NewItems;
        dtExchangeDate.SelectedDate = DateTime.Now;
    }

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
            ReturnItems.Clear();
            foreach (var d in details)
            {
                var part = d.Partid.HasValue ? await _partRepo.GetByIdAsync(d.Partid.Value) : null;
                ReturnItems.Add(new ExchangeReturnItem
                {
                    IsReturn = true,
                    Partid = d.Partid,
                    PartName = part?.Name ?? "",
                    OrigAmount = d.Amount ?? 0,
                    ReturnAmount = d.Amount ?? 0,
                    Price = d.Price ?? 0
                });
            }
            UpdateTotals();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询原单失败: {ex.Message}", "错误");
        }
    }

    private void BtnAddPart_Click(object sender, RoutedEventArgs e)
    {
        var selector = new PartSelectorWindow(App.ServiceProvider.GetRequiredService<IPartRepository>(), App.ServiceProvider.GetRequiredService<IPartQueryService>()) { Owner = Window.GetWindow(this) };
        if (selector.ShowDialog() == true && selector.SelectedParts.Count > 0)
        {
            var p = selector.SelectedParts[0];
            NewItems.Add(new ExchangeNewItem
            {
                Partid = p.Partid,
                PartName = p.Name,
                Amount = 1,
                Price = p.Lsprice ?? 0
            });
            UpdateTotals();
        }
    }

    private void BtnRemovePart_Click(object sender, RoutedEventArgs e)
    {
        if (dgNewItems.SelectedItem is ExchangeNewItem item)
        {
            NewItems.Remove(item);
            UpdateTotals();
        }
    }

    private void UpdateTotals()
    {
        var returnTotal = ReturnItems.Where(d => d.IsReturn).Sum(d => d.ReturnTotal);
        var newTotal = NewItems.Sum(d => d.LineTotal);
        var diff = newTotal - returnTotal;

        txtReturnTotal.Text = returnTotal.ToString("C2");
        txtNewTotal.Text = newTotal.ToString("C2");
        txtDiffAmount.Text = diff.ToString("C2");
        txtDiffAmount.Foreground = diff >= 0 ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Green;
    }

    private async void BtnConfirmExchange_Click(object sender, RoutedEventArgs e)
    {
        var returnList = ReturnItems.Where(d => d.IsReturn && d.ReturnAmount > 0).ToList();
        if (returnList.Count == 0 && NewItems.Count == 0)
        {
            MessageBox.Show("请选择退回配件或添加换入配件", "提示");
            return;
        }

        var returnTotal = returnList.Sum(d => d.ReturnTotal);
        var newTotal = NewItems.Sum(d => d.LineTotal);
        var diff = newTotal - returnTotal;

        if (MessageBox.Show($"确认换货?\n退回: {returnTotal:C2}\n换入: {newTotal:C2}\n补差价: {diff:C2}", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        try
        {
            var snService = App.ServiceProvider.GetRequiredService<ISerialNumberService>();
            var exchangeSn = await snService.GenerateExchangeSN();
            txtExchangeSn.Text = exchangeSn;

            var bill = new BillSell
            {
                Sn = exchangeSn,
                Client = txtOrigSn.Text.Trim(),
                Total = diff,
                BillTotal = diff,
                DiscountRate = 1,
                Flag = 3,
                Memo = $"换货-原单:{txtOrigSn.Text.Trim()}"
            };
            await _sellRepo.InsertBillAsync(bill);

            foreach (var item in returnList)
            {
                await _sellRepo.InsertDetailAsync(new DetailSell
                {
                    Sn = exchangeSn,
                    Partid = item.Partid,
                    Amount = -(long)item.ReturnAmount,
                    Price = item.Price,
                    BillPrice = item.Price,
                    Stotal = -item.ReturnTotal,
                    Flag = (int)BusinessConstants.BillFlag.Voided,
                    DiscountRate = 1
                });

                if (item.Partid.HasValue)
                    await _partRepo.IncreaseStockAsync(item.Partid.Value, item.ReturnAmount);
            }

            foreach (var item in NewItems)
            {
                await _sellRepo.InsertDetailAsync(new DetailSell
                {
                    Sn = exchangeSn,
                    Partid = item.Partid,
                    Amount = (long)item.Amount,
                    Price = item.Price,
                    BillPrice = item.Price,
                    Stotal = item.LineTotal,
                    Flag = (int)BusinessConstants.BillFlag.Voided,
                    DiscountRate = 1
                });

                if (item.Partid.HasValue)
                    await _partRepo.DecreaseStockAsync(item.Partid.Value, item.Amount);
            }

            MessageBox.Show($"换货成功!\n换货单号: {exchangeSn}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"换货失败: {ex.Message}", "错误");
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
