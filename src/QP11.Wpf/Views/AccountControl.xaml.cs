using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QP11.Core.Entities;
using QP11.Wpf.ViewModels;

namespace QP11.Wpf.Views;

public partial class AccountControl : UserControl, ITabContent
{
    private readonly AccountViewModel _viewModel;
    private List<dynamic> _accountList = new();

    public string TabTitle { get; }
    public bool HasUnsavedChanges => false;
    public event EventHandler? RequestClose;

    public AccountControl(AccountViewModel viewModel, string title = "现金账")
    {
        InitializeComponent();
        _viewModel = viewModel;
        TabTitle = title;
        dtStart.SelectedDate = DateTime.Now.AddDays(-90);
        dtEnd.SelectedDate = DateTime.Now;
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadAccountsAsync();
    }

    private async System.Threading.Tasks.Task LoadAccountsAsync()
    {
        try
        {
            int? flag = null;
            if (cboFlag.SelectedIndex == 1) flag = 1;
            else if (cboFlag.SelectedIndex == 2) flag = 0;

            var data = (await _viewModel.LoadIncomeExpenseListAsync(dtStart.SelectedDate, dtEnd.SelectedDate, flag)).ToList();
            _accountList = data;

            foreach (var row in data)
            {
                var f = (int?)row.flag;
                row.flag_type = f == 1 ? "收入" : "支出";
            }

            dgAccounts.ItemsSource = data;

            txtCount.Text = $"共 {data.Count} 条记录";
            decimal sumIncome = 0m;
            decimal sumExpense = 0m;
            foreach (var r in data)
            {
                if (r.income != null) sumIncome += Convert.ToDecimal(r.income);
                if (r.expense != null) sumExpense += Convert.ToDecimal(r.expense);
            }
            txtSumIncome.Text = sumIncome.ToString("N2");
            txtSumExpense.Text = sumExpense.ToString("N2");
            txtBalance.Text = (sumIncome - sumExpense).ToString("N2");

            ApplyRowColors();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载账目失败: {ex.Message}", "错误");
        }
    }

    private void ApplyRowColors()
    {
        for (int i = 0; i < dgAccounts.Items.Count; i++)
        {
            var row = (DataGridRow)dgAccounts.ItemContainerGenerator.ContainerFromIndex(i);
            if (row == null) continue;
            var item = dgAccounts.Items[i] as dynamic;
            var f = (int?)item.flag;
            row.Foreground = f == 0 ? Brushes.Blue : Brushes.Black;
        }
    }

    private object? GetSelectedAccount()
    {
        return dgAccounts.SelectedItem;
    }

    private async void DgAccounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var sel = GetSelectedAccount();
        if (sel == null) return;
        dynamic row = sel;
        var sn = (string?)row.sn;
        var name = (string?)row.name;
        if (string.IsNullOrEmpty(sn)) return;

        try
        {
            if (name != null && (name.Contains("采购") || name.Contains("应付款")))
            {
                var details = await _viewModel.LoadBuyDetailsAsync(sn);
                dgDetail.ItemsSource = details.Select(d => new
                {
                    d.Partno, d.Name, d.Amount, d.Unit, d.Cartype,
                    Price = d.Inprice, d.Stotal, d.Memo
                }).ToList();
            }
            else
            {
                var details = await _viewModel.LoadSellDetailsAsync(sn);
                dgDetail.ItemsSource = details.Select(d => new
                {
                    d.Partno, d.Name, d.Amount, d.Unit, d.Cartype,
                    Price = d.Price, d.Stotal, d.Memo
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载明细失败: {ex.Message}", "错误");
        }
    }

    private void BtnQuery_Click(object sender, RoutedEventArgs e) => _ = LoadAccountsAsync();

    private async void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AccountEditDialog();
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var entity = new Account
                {
                    Name = dialog.AccountName,
                    Je = dialog.Amount,
                    Flag = dialog.FlagValue,
                    Type = dialog.AccountType,
                    Memo = dialog.Memo
                };
                await _viewModel.InsertAccountAsync(entity);
                await LoadAccountsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"新增失败: {ex.Message}", "错误");
            }
        }
    }

    private async void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        var sel = GetSelectedAccount();
        if (sel == null) return;
        dynamic row = sel;
        var id = (long?)row.id;
        if (id == null) return;

        try
        {
            var entity = await _viewModel.GetAccountByIdAsync(id.Value);
            if (entity == null) return;

            var dialog = new AccountEditDialog(entity);
            if (dialog.ShowDialog() == true)
            {
                entity.Name = dialog.AccountName;
                entity.Je = dialog.Amount;
                entity.Flag = dialog.FlagValue;
                entity.Type = dialog.AccountType;
                entity.Memo = dialog.Memo;
                await _viewModel.UpdateAccountAsync(entity);
                await LoadAccountsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"编辑失败: {ex.Message}", "错误");
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var sel = GetSelectedAccount();
        if (sel == null) return;
        dynamic row = sel;
        var name = (string?)row.name;
        if (MessageBox.Show($"确定删除账目 [{name}]?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        try
        {
            var id = (long?)row.id;
            if (id == null) return;
            var entity = await _viewModel.GetAccountByIdAsync(id.Value);
            if (entity == null) return;
            entity.Flag = -1;
            await _viewModel.UpdateAccountAsync(entity);
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除失败: {ex.Message}", "错误");
        }
    }

    #region ITabContent

    public void OnAdd() => BtnAdd_Click(this, new RoutedEventArgs());
    public void OnEdit() => BtnEdit_Click(this, new RoutedEventArgs());
    public async void OnQuery() => await LoadAccountsAsync();
    public void OnDelete() => BtnDelete_Click(this, new RoutedEventArgs());
    public void OnSave() { }
    public async void OnSettle() => await LoadAccountsAsync();
    public void OnPrint() { }
    public void OnReturn() { }
    public void OnCancel() { }
    public void OnHistory() { }
    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);

    #endregion
}
