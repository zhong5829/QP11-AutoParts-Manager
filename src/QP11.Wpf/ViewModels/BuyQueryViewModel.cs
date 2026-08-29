using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QP11.Core.Constants;
using QP11.Core.Interfaces;

namespace QP11.Wpf.ViewModels;

/// <summary>
/// 采购单据查询 ViewModel - 承担业务逻辑，与 UI 解耦
/// </summary>
public class BuyQueryViewModel : BaseViewModel
{
    private readonly IBuyRepository _buyRepo;
    private readonly IPartRepository _partRepo;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IArrearageRepository _arrearRepo;

    public BuyQueryViewModel(IBuyRepository buyRepo, IPartRepository partRepo, IUnitOfWorkFactory uowFactory, IArrearageRepository arrearRepo)
    {
        _buyRepo = buyRepo;
        _partRepo = partRepo;
        _uowFactory = uowFactory;
        _arrearRepo = arrearRepo;
    }

    /// <summary>
    /// 加载采购单据列表
    /// </summary>
    public async Task<IEnumerable<dynamic>> LoadBillListAsync(DateTime? startDate, DateTime? endDate, string? supplier, string? worker)
    {
        return await _buyRepo.GetBillListAsync(startDate, endDate, supplier, worker);
    }

    /// <summary>
    /// 作废采购单据 - 物理删除该单（明细+单据头+欠款）并回补库存（撤销入库）
    /// 与旧系统"作废=直接删除数据"一致；不再写 flag=3（bill_buy 中 3 已被借货"在借"占用）
    /// </summary>
    public async Task VoidBillAsync(string sn)
    {
        var details = await _buyRepo.GetDetailsAsync(sn);

        using var uow = _uowFactory.Create();
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;
            var dbConn = uow.Connection;

            foreach (var d in details)
            {
                if (d.Partid.HasValue && (d.Amount ?? 0) > 0)
                    await _partRepo.DecreaseStockAsync(d.Partid.Value, d.Amount ?? 0, txn, dbConn);
            }

            await _buyRepo.DeleteDetailsBySnAsync(sn, txn);
            await _buyRepo.DeleteBillAsync(sn, txn);
            await _arrearRepo.DeleteBySnAsync(sn, txn);

            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }
}
