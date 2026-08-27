using System;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

/// <summary>
/// 收款录入窗口，记录销售收款和其他收入
/// </summary>
public partial class PaymentEntryWindow : Window
{
    private readonly IAccountRepository _accountRepo;
    private readonly IPaysRepository _paysRepo;

    public PaymentEntryWindow(IAccountRepository accountRepo, IPaysRepository paysRepo)
    {
        _accountRepo = accountRepo;
        _paysRepo = paysRepo;
        InitializeComponent();
        LoadAccounts();
    }

    /// <summary>
    /// 加载所有账户到下拉框
    /// </summary>
    private async void LoadAccounts()
    {
        try
        {
            var accounts = await _accountRepo.GetAllAsync();
            cboAccount.ItemsSource = accounts;
            if (accounts.Any()) cboAccount.SelectedIndex = 0;
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载账户失败"); MessageBox.Show($"加载账户失败: {ex.Message}", "错误"); }
    }

    /// <summary>
    /// 确认收款：更新账户余额并记录收支明细
    /// </summary>
    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var account = cboAccount.SelectedItem as Account;
        if (account == null) { MessageBox.Show("请选择收款账户", "提示"); return; }
        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0) { MessageBox.Show("请输入有效金额", "提示"); return; }

        try
        {
            await _accountRepo.UpdateBalanceAsync(account.Id, amount);
            await _paysRepo.InsertAsync(new Pays
            {
                AccountId = account.Id,
                Type = cboType.Text,
                Je = amount,
                Sn = txtSn.Text.Trim(),
                Memo = txtMemo.Text.Trim()
            });
            DialogResult = true;
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "收款失败"); MessageBox.Show($"收款失败: {ex.Message}", "错误"); }
    }
}
