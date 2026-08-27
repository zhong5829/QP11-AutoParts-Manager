using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

/// <summary>
/// 转采购明细行（可编辑到货数量/采购价）
/// </summary>
public class JhdhConvertDetailItem : INotifyPropertyChanged
{
    public long PartId { get; set; }
    public string? PartNo { get; set; }
    public string? Name { get; set; }
    public string? Cartype { get; set; }
    public string? Unit { get; set; }
    public long PlanAmount { get; set; }

    private decimal _inPrice;
    public decimal InPrice
    {
        get => _inPrice;
        set { _inPrice = value; OnPropertyChanged(nameof(InPrice)); OnPropertyChanged(nameof(SubTotal)); }
    }

    private decimal _lsPrice;
    public decimal LsPrice
    {
        get => _lsPrice;
        set => _lsPrice = value;
    }

    private decimal _amount;
    public decimal Amount
    {
        get => _amount;
        set { _amount = value; OnPropertyChanged(nameof(Amount)); OnPropertyChanged(nameof(SubTotal)); }
    }

    public decimal SubTotal => Math.Round(InPrice * Amount, 2);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class JhdhConvertWindow : Window
{
    private readonly string _jhdhSn;
    private readonly IJhdhService _jhdhService;
    public ObservableCollection<JhdhConvertDetailItem> ConvertDetails { get; } = new();
    public string? BuySn { get; private set; }

    public JhdhConvertWindow(string jhdhSn, List<JhdhDetailItem> planDetails, IJhdhService jhdhService)
    {
        _jhdhSn = jhdhSn;
        _jhdhService = jhdhService;

        InitializeComponent();
        txtJhdhSn.Text = jhdhSn;

        foreach (var d in planDetails)
        {
            var item = new JhdhConvertDetailItem
            {
                PartId = d.PartId,
                PartNo = d.PartNo,
                Name = d.Name,
                Cartype = d.Cartype,
                Unit = d.Unit,
                PlanAmount = (long)d.Amount,
                Amount = d.Amount,      // 默认到货数 = 计划数
                InPrice = d.InPrice,    // 默认采购价 = 计划价
                LsPrice = d.LsPrice
            };
            item.PropertyChanged += (_, _) => UpdateTotal();
            ConvertDetails.Add(item);
        }

        ConvertDetails.CollectionChanged += OnConvertDetailsChanged;
        dgConvertDetails.ItemsSource = ConvertDetails;
        UpdateTotal();
    }

    private void OnConvertDetailsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in e.NewItems?.Cast<JhdhConvertDetailItem>() ?? Array.Empty<JhdhConvertDetailItem>())
            item.PropertyChanged += (_, _) => UpdateTotal();
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        var total = ConvertDetails.Sum(d => d.SubTotal);
        if (txtTotal == null) return;
        txtTotal.Text = total.ToString("N2");
        UpdateCredit();
    }

    private void Payment_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateCredit();
    }

    private void UpdateCredit()
    {
        if (txtCash == null || txtCredit == null) return;
        var total = ConvertDetails.Sum(d => d.SubTotal);
        var cash = decimal.TryParse(txtCash.Text, out var c) ? c : 0;
        var credit = Math.Max(0, total - cash);
        txtCredit.Text = credit.ToString("N2");
    }

    private async void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (ConvertDetails.Count == 0)
        {
            MessageBox.Show("明细为空", "提示");
            return;
        }

        if (ConvertDetails.Any(d => d.Amount <= 0))
        {
            MessageBox.Show("到货数量必须大于0", "提示");
            return;
        }

        if (MessageBox.Show("确认转采购入库?\n入库后将增加库存数量，计划单状态变为已执行。",
            "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        try
        {
            IsEnabled = false;

            var cash = decimal.TryParse(txtCash.Text, out var c) ? c : 0;
            var credit = decimal.TryParse(txtCredit.Text, out var cr) ? cr : 0;

            // 构造 DetailBuy 列表
            var buyDetails = ConvertDetails.Select(d => new DetailBuy
            {
                Partid = d.PartId,
                Partno = d.PartNo,
                Name = d.Name,
                Cartype = d.Cartype,
                Unit = d.Unit,
                Inprice = d.InPrice,
                Lsprice = d.LsPrice,
                Amount = (long)d.Amount,
                Memo = $"由计划单{_jhdhSn}转入"
            }).ToList();

            BuySn = await _jhdhService.ConvertToBuyOrderAsync(_jhdhSn, buyDetails, cash, credit);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"转采购入库失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
