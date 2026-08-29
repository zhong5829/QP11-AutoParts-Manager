using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.ViewModels;

/// <summary>
/// 销售明细查询 ViewModel - 承担业务逻辑，与 UI 解耦
/// </summary>
public class SellQueryViewModel : BaseViewModel
{
    private readonly ISellRepository _sellRepo;
    private readonly IPartRepository _partRepo;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IArrearageRepository _arrearRepo;

    public SellQueryViewModel(ISellRepository sellRepo, IPartRepository partRepo, IUnitOfWorkFactory uowFactory, IArrearageRepository arrearRepo)
    {
        _sellRepo = sellRepo;
        _partRepo = partRepo;
        _uowFactory = uowFactory;
        _arrearRepo = arrearRepo;
    }

    /// <summary>
    /// 作废销售单据 - 物理删除该单（明细+单据头+欠款）并回补库存
    /// 与旧系统"作废=直接删除数据"一致：作废后销售历史/报表中不再出现该记录
    /// </summary>
    public async Task VoidBillAsync(string sn)
    {
        var details = (await _sellRepo.GetDetailsAsync(sn)).ToList();

        using var uow = _uowFactory.Create();
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;
            var dbConn = uow.Connection;

            foreach (var d in details)
            {
                if (d.Partid.HasValue && (d.Amount ?? 0m) > 0)
                    await _partRepo.IncreaseStockAsync(d.Partid.Value, d.Amount ?? 0m, txn, dbConn);
            }

            await _sellRepo.DeleteDetailsAsync(sn, txn);
            await _sellRepo.DeleteBillAsync(sn, txn);
            await _arrearRepo.DeleteBySnAsync(sn, txn);

            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 加载销售明细列表
    /// </summary>
    public async Task<IEnumerable<dynamic>> LoadDetailListAsync(DateTime? startDate, DateTime? endDate, string? client, string? worker)
    {
        return await _sellRepo.GetDetailListAsync(startDate, endDate, client, worker);
    }

    /// <summary>
    /// 更新备注
    /// </summary>
    public async Task UpdateMemoAsync(string sn, string memo)
    {
        await _sellRepo.UpdateMemoAsync(sn, memo);
    }

    /// <summary>
    /// 批量做账
    /// </summary>
    public async Task<int> BatchSettleArrearAsync(IEnumerable<string> sns, string payMethod)
    {
        return await _sellRepo.BatchSettleArrearAsync(sns, payMethod);
    }

    /// <summary>
    /// 获取挂账单据
    /// </summary>
    public async Task<IEnumerable<ArrearBillInfo>> GetArrearBillsAllAsync(IEnumerable<string> sns)
    {
        return await _sellRepo.GetArrearBillsAllAsync(sns);
    }
}
