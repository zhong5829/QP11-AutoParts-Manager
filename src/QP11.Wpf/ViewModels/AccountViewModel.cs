using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.ViewModels;

/// <summary>
/// 账目管理 ViewModel - 承担业务逻辑，与 UI 解耦
/// </summary>
public class AccountViewModel : BaseViewModel
{
    private readonly IAccountRepository _accountRepo;
    private readonly IBuyRepository _buyRepo;
    private readonly ISellRepository _sellRepo;

    public AccountViewModel(IAccountRepository accountRepo, IBuyRepository buyRepo, ISellRepository sellRepo)
    {
        _accountRepo = accountRepo;
        _buyRepo = buyRepo;
        _sellRepo = sellRepo;
    }

    /// <summary>
    /// 加载收支列表
    /// </summary>
    public async Task<IEnumerable<dynamic>> LoadIncomeExpenseListAsync(DateTime? startDate, DateTime? endDate, int? flag)
    {
        return await _accountRepo.GetIncomeExpenseListAsync(startDate, endDate, flag);
    }

    /// <summary>
    /// 新增账目
    /// </summary>
    public async Task InsertAccountAsync(Account entity)
    {
        await _accountRepo.InsertAsync(entity);
    }

    /// <summary>
    /// 获取账目
    /// </summary>
    public async Task<Account?> GetAccountByIdAsync(long id)
    {
        return await _accountRepo.GetByIdAsync(id);
    }

    /// <summary>
    /// 更新账目
    /// </summary>
    public async Task UpdateAccountAsync(Account entity)
    {
        await _accountRepo.UpdateAsync(entity);
    }

    /// <summary>
    /// 加载采购明细
    /// </summary>
    public async Task<IEnumerable<DetailBuy>> LoadBuyDetailsAsync(string sn)
    {
        return await _buyRepo.GetDetailsAsync(sn);
    }

    /// <summary>
    /// 加载销售明细
    /// </summary>
    public async Task<IEnumerable<DetailSell>> LoadSellDetailsAsync(string sn)
    {
        return await _sellRepo.GetDetailsAsync(sn);
    }
}
