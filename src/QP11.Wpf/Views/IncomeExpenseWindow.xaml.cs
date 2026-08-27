using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

/// <summary>
/// 收支明细窗口，按账户和日期范围查看收支记录
/// </summary>
public partial class IncomeExpenseWindow : Window
{
    private readonly IAccountRepository _accountRepo;
    private readonly IPaysRepository _paysRepo;

    public IncomeExpenseWindow(IAccountRepository accountRepo, IPaysRepository paysRepo)
    {
        _accountRepo = accountRepo;
        _paysRepo = paysRepo;
        InitializeComponent();
        dtStart.SelectedDate = DateTime.Now.AddDays(-30);
        dtEnd.SelectedDate = DateTime.Now;
        LoadAccounts();
    }

    /// <summary>
    /// 加载账户列表到下拉框
    /// </summary>
    private async void LoadAccounts()
    {
        try
        {
            var accounts = await _accountRepo.GetAllAsync();
            cboAccount.ItemsSource = accounts;
            cboAccount.DisplayMemberPath = "Name";
            cboAccount.SelectedValuePath = "Id";
            if (accounts.Any()) cboAccount.SelectedIndex = 0;
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载账户失败"); MessageBox.Show($"加载账户失败: {ex.Message}", "错误"); }
    }

    /// <summary>
    /// 账户选择变化时自动加载收支记录
    /// </summary>
    private async void CboAccount_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadPays();
    }

    /// <summary>
    /// 按账户和日期范围加载收支明细
    /// </summary>
    private async System.Threading.Tasks.Task LoadPays()
    {
        if (cboAccount.SelectedValue is not long accountId) return;
        try
        {
            var data = await _paysRepo.GetByAccountAsync(accountId, dtStart.SelectedDate, dtEnd.SelectedDate);
            dgPays.ItemsSource = data;
            var list = data.ToList();
            txtCount.Text = $"共 {list.Count} 条记录";
            txtTotal.Text = $"合计: {list.Sum(p => p.Je ?? 0m):C2}";
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "查询收支记录失败"); MessageBox.Show($"查询失败: {ex.Message}", "错误"); }
    }

    /// <summary>
    /// 查询按钮点击
    /// </summary>
    private async void BtnSearch_Click(object sender, RoutedEventArgs e) => await LoadPays();
}
