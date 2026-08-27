using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

/// <summary>
/// 应收应付管理窗口，提供按类型和日期范围查询欠款记录及合计功能
/// </summary>
public partial class ArrearageWindow : Window
{
    private readonly IArrearageRepository _arrearageRepo;
    private List<dynamic> _rows = new();

    public ArrearageWindow(IArrearageRepository arrearageRepo)
    {
        _arrearageRepo = arrearageRepo;
        InitializeComponent();
        dtStart.SelectedDate = DateTime.Now.AddDays(-90);
        dtEnd.SelectedDate = DateTime.Now;
        LoadArrearages();
    }

    /// <summary>
    /// 按类型和日期范围加载欠款记录（含退货取反和未付金额）
    /// </summary>
    private async void LoadArrearages()
    {
        try
        {
            int? type = null;
            if (cboType.SelectedIndex == 1) type = 1;
            else if (cboType.SelectedIndex == 2) type = 2;

            _rows = (await _arrearageRepo.GetListWithCalcAsync(type, dtStart.SelectedDate, dtEnd.SelectedDate)).ToList();
            dgArrearage.ItemsSource = _rows;

            var totalOwe = _rows.Sum(r => (decimal)r.owe);
            txtCount.Text = $"共 {_rows.Count} 条记录";
            txtTotal.Text = $"欠款合计: {totalOwe:N2}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询失败: {ex.Message}", "错误");
        }
    }

    private void DgArrearage_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is IDictionary<string, object> row && row.ContainsKey("is_return"))
        {
            var isReturn = Convert.ToInt32(row["is_return"]) == 1;
            if (isReturn) e.Row.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    /// <summary>
    /// 查询按钮点击
    /// </summary>
    private void BtnSearch_Click(object sender, RoutedEventArgs e) => LoadArrearages();
}
