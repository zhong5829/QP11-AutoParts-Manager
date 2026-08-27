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

    public BuyQueryViewModel(IBuyRepository buyRepo, IPartRepository partRepo)
    {
        _buyRepo = buyRepo;
        _partRepo = partRepo;
    }

    /// <summary>
    /// 加载采购单据列表
    /// </summary>
    public async Task<IEnumerable<dynamic>> LoadBillListAsync(DateTime? startDate, DateTime? endDate, string? supplier, string? worker)
    {
        return await _buyRepo.GetBillListAsync(startDate, endDate, supplier, worker);
    }

    /// <summary>
    /// 作废采购单据（扣减库存+更新状态）
    /// </summary>
    public async Task VoidBillAsync(string sn)
    {
        var details = await _buyRepo.GetDetailsAsync(sn);
        foreach (var d in details)
        {
            if (d.Partid.HasValue && (d.Amount ?? 0) > 0)
                await _partRepo.DecreaseStockAsync(d.Partid.Value, d.Amount ?? 0);
        }
        await _buyRepo.UpdateBillStatusAsync(sn, (int)BusinessConstants.BillFlag.Voided);
    }
}
