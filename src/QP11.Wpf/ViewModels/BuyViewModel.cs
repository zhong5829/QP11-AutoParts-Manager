using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Exceptions;
using QP11.Core.Interfaces;
using QP11.Core.Models;
using QP11.Wpf.Helpers;
using QP11.Wpf.Views;

namespace QP11.Wpf.ViewModels;

/// <summary>
/// 采购开单 ViewModel - 承担业务逻辑，与 UI 解耦
/// </summary>
public class BuyViewModel : BaseViewModel
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IBuyRepository _buyRepo;
    private readonly IPartRepository _partRepo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly IUserRepository _userRepo;
    private readonly ISerialNumberService _snService;
    private readonly IArrearageRepository _arrearRepo;

    public ObservableCollection<BuyDetailItem> Details { get; } = new();

    public BuyViewModel(
        IDbConnectionFactory dbFactory,
        IUnitOfWorkFactory uowFactory,
        IBuyRepository buyRepo,
        IPartRepository partRepo,
        ISupplierRepository supplierRepo,
        IUserRepository userRepo,
        ISerialNumberService snService,
        IArrearageRepository arrearRepo)
    {
        _dbFactory = dbFactory;
        _uowFactory = uowFactory;
        _buyRepo = buyRepo;
        _partRepo = partRepo;
        _supplierRepo = supplierRepo;
        _userRepo = userRepo;
        _snService = snService;
        _arrearRepo = arrearRepo;
    }

    /// <summary>
    /// 加载供应商列表
    /// </summary>
    public async Task<List<SupplierInfor>> LoadSuppliersAsync()
    {
        var suppliers = await _supplierRepo.GetAllAsync();
        return suppliers.ToList();
    }

    /// <summary>
    /// 加载业务员列表
    /// </summary>
    public async Task<IEnumerable<UserInfor>> LoadUsersAsync()
    {
        return await _userRepo.GetAllAsync();
    }

    /// <summary>
    /// 加载单据列表
    /// </summary>
    public async Task<List<BuyBillDisplay>> LoadBillListAsync(int currentFlag)
    {
        using var db = await _dbFactory.CreateAsync();
        var sql = @"SELECT bill_buy.sn AS Sn, bill_buy.datetime AS Datetime,
                    bill_buy.supplier AS Supplier, bill_buy.worker AS Worker,
                    bill_buy.total AS Total, bill_buy.flag AS Flag,
                    ISNULL(supplier_infor.name, bill_buy.supplier) AS SupplierName,
                    ISNULL(work_infor.name, bill_buy.worker) AS WorkerName
                    FROM bill_buy
                    LEFT JOIN supplier_infor ON supplier_infor.sid = bill_buy.supplier
                    LEFT JOIN work_infor ON work_infor.workid = bill_buy.worker
                    WHERE bill_buy.flag = @Flag
                    ORDER BY bill_buy.datetime DESC";

        var data = (await db.QueryAsync<dynamic>(sql, new { Flag = currentFlag })).ToList();
        return data.Select(b => new BuyBillDisplay
        {
            Sn = (string?)b.Sn,
            Datetime = (DateTime?)b.Datetime,
            SupplierName = (string?)b.SupplierName,
            WorkerName = (string?)b.WorkerName,
            Total = (decimal?)b.Total,
            Flag = (int?)b.Flag ?? 0,
            FlagText = ((int?)b.Flag ?? 0) switch
            {
                0 => "未结算",
                1 => "已结算",
                2 => "退货",
                3 => "已作废",
                _ => "未知"
            }
        }).ToList();
    }

    /// <summary>
    /// 根据单号加载采购单
    /// </summary>
    public async Task<BillBuy?> LoadBillAsync(string sn)
    {
        return await _buyRepo.GetBySnAsync(sn);
    }

    /// <summary>
    /// 加载采购单明细
    /// </summary>
    public async Task<IEnumerable<DetailBuy>> LoadDetailsAsync(string sn)
    {
        return await _buyRepo.GetDetailsAsync(sn);
    }

    /// <summary>
    /// 根据业务员名称查找工号
    /// </summary>
    public async Task<string> ResolveWorkerIdAsync(string workerName)
    {
        return await _buyRepo.ResolveWorkerIdAsync(workerName);
    }

    /// <summary>
    /// 根据供应商ID查找供应商名称
    /// </summary>
    public async Task<string?> GetSupplierNameAsync(string sid)
    {
        return await _supplierRepo.GetNameBySidAsync(sid);
    }

    /// <summary>
    /// 根据工号查找业务员名称
    /// </summary>
    public async Task<string?> GetWorkerNameAsync(string workid)
    {
        return await _buyRepo.GetWorkerNameAsync(workid);
    }

    /// <summary>
    /// 保存采购单（事务化：更新单头 + 删旧明细 + 插新明细 + 更新欠款）
    /// </summary>
    public async Task SaveBillAsync(
        string? existingBillNo,
        BillBuy bill,
        List<BuyDetailItem> details)
    {
        if (details.Any(d => d.Amount <= 0))
            throw new BusinessRuleException("采购明细数量必须大于 0，不能保存数量为 0 或负数的明细");

        using var uow = _uowFactory.Create();
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;

            if (!string.IsNullOrEmpty(existingBillNo))
            {
                bill.Sn = existingBillNo;
                await _buyRepo.UpdateAsync(bill, txn);

                // 删除旧欠款记录，防止重复累加
                await _arrearRepo.DeleteBySnAsync(existingBillNo, txn);

                // 删除旧明细
                await _buyRepo.DeleteDetailsBySnAsync(existingBillNo, txn);
            }
            else
            {
                bill.Cash ??= 0m;
                bill.Checks ??= 0m;
                await _buyRepo.InsertBillAsync(bill, txn);
            }

            // 批量查询配件主档，补全 class/unit/place/保质期等字段
            var partIds = details.Where(d => d.PartId > 0).Select(d => d.PartId).Distinct().ToList();
            var partMap = partIds.Count > 0 ? await _partRepo.GetByIdsAsync(partIds) : new Dictionary<long, PartData>();

            var detailList = details.Select(d => {
                var partInfo = d.PartId > 0 && partMap.TryGetValue(d.PartId, out var pd) ? pd : null;
                return new DetailBuy
                {
                    Sn = bill.Sn,
                    Partid = d.PartId,
                    Partno = d.PartNo,
                    Name = d.PartName,
                    Carname = d.CarName,
                    Cartype = d.Cartype,
                    Unit = !string.IsNullOrEmpty(d.Unit) ? d.Unit : partInfo?.Unit,
                    Amount = (long)d.Amount,
                    Inprice = d.InPrice,
                    Lsprice = d.LsPrice,
                    Pfprice = d.PfPrice,
                    Stotal = d.SubTotal,
                    Place = !string.IsNullOrEmpty(d.Place) ? d.Place : partInfo?.Place,
                    Class = partInfo?.ClassName,
                    Datetime = bill.Datetime,
                    Memo = d.Memo,
                    PartGg = partInfo?.PartGg,
                    PartTh = partInfo?.PartTh,
                    PartCclb = partInfo?.PartCclb,
                    PartBzq = partInfo?.PartBzq,
                    PartBzrq = partInfo?.PartBzrq
                };
            }).ToList();

            await _buyRepo.InsertDetailsAsync(detailList, txn);

            // 重新插入欠款记录（先删后插，防止重复）
            if (bill.Arrear > 0.01m)
            {
                await _arrearRepo.InsertAsync(new Arrearage
                {
                    Bid = bill.Supplier,
                    Type = 1,
                    Btype = 1,
                    Total = bill.Arrear ?? 0,
                    Sn = bill.Sn
                }, txn);
            }

            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 结算采购单（增加库存、更新状态）
    /// </summary>
    public async Task SettleBillAsync(
        string sn,
        List<BuyDetailItem> details,
        List<BuyDetailItem> newParts,
        List<NameDiffUpdate> pendingNameUpdates)
    {
        if (details.Any(d => d.Amount <= 0))
            throw new BusinessRuleException("采购明细数量必须大于 0，不能结算数量为 0 或负数的明细");

        using var uow = _uowFactory.Create();
        await uow.BeginTransactionAsync();
        var txn = uow.Transaction;
        var dbConn = uow.Connection;

        try
        {
            if (pendingNameUpdates.Count > 0)
            {
                await UpdatePartNamesAsync(pendingNameUpdates, txn);
            }

            // 新配件：创建配件记录 + 直接写入正确库存数量
            if (newParts.Count > 0)
            {
                foreach (var item in newParts)
                {
                    var namePy = PinyinHelper.GetPinyinInitials(item.PartName ?? "");
                    var cartypePy = PinyinHelper.GetPinyinInitials(item.Cartype ?? "");
                    var place = string.IsNullOrEmpty(item.Place) ? "" : item.Place.Trim();
                    if (place == "废品仓") place = "";

                    var entity = new PartData
                    {
                        Partno = item.PartNo?.Trim(),
                        Name = item.PartName?.Trim(),
                        Carname = item.CarName?.Trim(),
                        Cartype = item.Cartype?.Trim(),
                        Inprice = item.InPrice,
                        Lsprice = item.LsPrice,
                        Pfprice = item.PfPrice,
                        NamePy = namePy,
                        CartypePy = cartypePy,
                        Area = place
                    };

                    await _partRepo.InsertAsync(entity, txn);
                    item.PartId = entity.Partid;

                    var stockSql = @"INSERT INTO part_stock (partid, place, amount, lsprice, pfprice)
                                     VALUES (@PartId, @Place, @Amount, @LsPrice, @PfPrice)";
                    await txn.Connection!.ExecuteAsync(stockSql, new
                    {
                        PartId = entity.Partid,
                        Place = place,
                        Amount = item.Amount,
                        LsPrice = item.LsPrice,
                        PfPrice = item.PfPrice
                    }, txn);
                }
            }

            // 已有配件：更新库存数量，并同步本单零售价/批发价到配件档案
            foreach (var item in details)
            {
                if (item.PartId > 0 && !newParts.Contains(item))
                {
                    await _partRepo.IncreaseStockAsync(item.PartId, item.Amount, txn, dbConn);
                    if (item.LsPrice > 0 || item.PfPrice > 0)
                        await _partRepo.UpdatePricesAsync(item.PartId, item.LsPrice, item.PfPrice, txn, dbConn);
                }
            }

            // 更新单据状态为已入库
            await _buyRepo.UpdateBillStatusAsync(sn, (int)BusinessConstants.BillFlag.Confirmed, txn);

            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 更新配件名称
    /// </summary>
    public async Task UpdatePartNamesAsync(List<NameDiffUpdate> updates, IDbTransaction? txn = null)
    {
        var db = txn?.Connection ?? await _dbFactory.CreateAsync();
        foreach (var u in updates)
        {
            var namePy = PinyinHelper.GetPinyinInitials(u.NewName ?? "");
            await db.ExecuteAsync(
                "UPDATE part_data SET name = @Name, name_py = @NamePy WHERE partid = @PartId",
                new { Name = u.NewName, NamePy = namePy, PartId = u.PartId }, txn);
        }
        if (txn == null) db.Dispose();
    }

    /// <summary>
    /// 生成采购单号
    /// </summary>
    public async Task<string> GenerateBuySNAsync()
    {
        return await _snService.GenerateBuySN();
    }
}
