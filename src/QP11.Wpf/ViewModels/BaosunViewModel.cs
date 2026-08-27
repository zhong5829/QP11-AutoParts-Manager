using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Services;
using QP11.Wpf.Views;

namespace QP11.Wpf.ViewModels;

/// <summary>
/// 报损管理 ViewModel - 承担业务逻辑，与 UI 解耦
/// </summary>
public class BaosunViewModel : BaseViewModel
{
    private readonly IBaosunRepository _baosunRepo;
    private readonly IPartRepository _partRepo;
    private readonly IUserRepository _userRepo;
    private readonly ISerialNumberService _snService;

    public ObservableCollection<BaosunDetailItem> Details { get; } = new();

    public BaosunViewModel(
        IBaosunRepository baosunRepo,
        IPartRepository partRepo,
        IUserRepository userRepo,
        ISerialNumberService snService)
    {
        _baosunRepo = baosunRepo;
        _partRepo = partRepo;
        _userRepo = userRepo;
        _snService = snService;
    }

    /// <summary>
    /// 加载业务员列表
    /// </summary>
    public async Task<IEnumerable<UserInfor>> LoadUsersAsync()
    {
        return await _userRepo.GetAllAsync();
    }

    /// <summary>
    /// 加载配件库存列表（支持多条件实时搜索）
    /// </summary>
    public async Task<IEnumerable<Core.Models.PartStockDisplay>> LoadPartListAsync(string? partNo, string? partName, string? cartype)
    {
        if (string.IsNullOrWhiteSpace(partNo) && string.IsNullOrWhiteSpace(partName) && string.IsNullOrWhiteSpace(cartype))
            return await _partRepo.GetStockListAsync(null);
        return await _partRepo.GetStockListAdvancedAsync(partNo: partNo, partName: partName, cartype: cartype);
    }

    /// <summary>
    /// 加载报损单列表
    /// </summary>
    public async Task<IEnumerable<BillBaosun>> LoadBillListAsync(DateTime? startDate, DateTime? endDate)
    {
        return await _baosunRepo.GetListAsync(startDate, endDate);
    }

    /// <summary>
    /// 根据单号加载报损单
    /// </summary>
    public async Task<BillBaosun?> LoadBillAsync(string sn)
    {
        return await _baosunRepo.GetBySnAsync(sn);
    }

    /// <summary>
    /// 加载报损单明细
    /// </summary>
    public async Task<IEnumerable<DetailBaosun>> LoadDetailsAsync(string sn)
    {
        return await _baosunRepo.GetDetailsAsync(sn);
    }

    /// <summary>
    /// 保存报损单
    /// </summary>
    public async Task<string> SaveBillAsync(BillBaosun bill, ObservableCollection<BaosunDetailItem> details)
    {
        var billNo = await _snService.GenerateSellSN();
        bill.Sn = billNo;
        bill.Client = BusinessConstants.BaosunClientId;

        await _baosunRepo.InsertBillAsync(bill);

        foreach (var item in details)
        {
            var detail = new DetailBaosun
            {
                Sn = billNo,
                Partid = item.Partid,
                Partno = item.PartNo,
                Name = item.PartName,
                Amount = (long?)item.Amount,
                Unit = item.Unit,
                Cartype = item.Cartype,
                Cb = item.Cb,
                Inprice = item.Inprice,
                Intotal = item.SubTotal,
                Memo = item.Memo
            };
            await _baosunRepo.InsertDetailAsync(detail);

            if (item.Partid.HasValue)
            {
                await _partRepo.DecreaseStockAsync(item.Partid.Value, item.Amount);
            }
        }

        return billNo;
    }

    /// <summary>
    /// 审核报损单
    /// </summary>
    public async Task SettleBillAsync(string sn)
    {
        await _baosunRepo.UpdateBillStatusAsync(sn, (int)BusinessConstants.BillFlag.Confirmed);
    }
}
