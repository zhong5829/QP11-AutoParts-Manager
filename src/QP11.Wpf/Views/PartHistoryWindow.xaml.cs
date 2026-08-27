using System;
using System.Windows;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class PartHistoryWindow : Window
{
    private readonly IPartQueryService _partQuery;
    private readonly long _partId;
    private readonly string _partTitle;

    public PartHistoryWindow(IPartQueryService partQuery, long partId, string partno, string? name)
    {
        InitializeComponent();
        _partQuery = partQuery;
        _partId = partId;
        _partTitle = $"{partno} {(string.IsNullOrEmpty(name) ? "" : name)}";
        txtTitle.Text = _partTitle;

        KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; Close(); } };

        Loaded += async (_, _) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var buyHistory = await _partQuery.GetBuyHistoryAsync(_partId, 50);
            dgBuy.ItemsSource = buyHistory;

            var sellHistory = await _partQuery.GetSellHistoryAsync(_partId, top: 50);
            dgSell.ItemsSource = sellHistory;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "查询配件历史失败 PartId={PartId}", _partId);
            MessageBox.Show($"查询历史失败: {ex.Message}", "错误");
        }
    }
}
