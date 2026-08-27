using System;
using System.Linq;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

/// <summary>
/// 账户转账窗口，在账户之间进行资金划转
/// </summary>
public partial class AccountTransferWindow : Window
{
    private readonly IAccountRepository _accountRepo;
    private readonly IPaysRepository _paysRepo;

    public AccountTransferWindow(IAccountRepository accountRepo, IPaysRepository paysRepo)
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
            var accounts = (await _accountRepo.GetAllAsync()).ToList();
            cboFrom.ItemsSource = accounts;
            cboTo.ItemsSource = accounts.ToList();
            if (accounts.Count > 0) { cboFrom.SelectedIndex = 0; cboTo.SelectedIndex = Math.Min(1, accounts.Count - 1); }
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载账户失败"); MessageBox.Show($"加载账户失败: {ex.Message}", "错误"); }
    }

    /// <summary>
    /// 执行转账操作：扣减转出账户余额、增加转入账户余额、记录收支明细
    /// </summary>
    private async void BtnTransfer_Click(object sender, RoutedEventArgs e)
    {
        var fromAccount = cboFrom.SelectedItem as Account;
        var toAccount = cboTo.SelectedItem as Account;
        if (fromAccount == null || toAccount == null) { MessageBox.Show("请选择账户", "提示"); return; }
        if (fromAccount.Id == toAccount.Id) { MessageBox.Show("转出和转入账户不能相同", "提示"); return; }
        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0) { MessageBox.Show("请输入有效金额", "提示"); return; }

        if (MessageBox.Show($"确认从 [{fromAccount.Name}] 转账 {amount:C2} 到 [{toAccount.Name}]?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        try
        {
            await _accountRepo.UpdateBalanceAsync(fromAccount.Id, -amount);
            await _accountRepo.UpdateBalanceAsync(toAccount.Id, amount);
            await _paysRepo.InsertAsync(new Pays { AccountId = fromAccount.Id, Type = "转出", Je = -amount, Memo = $"转账到{toAccount.Name}" });
            await _paysRepo.InsertAsync(new Pays { AccountId = toAccount.Id, Type = "转入", Je = amount, Memo = $"来自{fromAccount.Name}" });
            DialogResult = true;
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "转账失败"); MessageBox.Show($"转账失败: {ex.Message}", "错误"); }
    }
}
