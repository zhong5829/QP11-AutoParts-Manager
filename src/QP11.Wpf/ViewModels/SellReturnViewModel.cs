using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

using QP11.Services;
using QP11.Wpf.Views;

namespace QP11.Wpf.ViewModels;

/// <summary>
/// 销售退货 ViewModel - 承担业务逻辑，与 UI 解耦
/// </summary>
public class SellReturnViewModel : BaseViewModel
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ISellRepository _sellRepo;
    private readonly IPartRepository _partRepo;
    private readonly IClientRepository _clientRepo;
    private readonly IUserRepository _userRepo;
    private readonly IBuyRepository _buyRepo;
    private readonly ISerialNumberService _snService;

    public ObservableCollection<SellReturnItem> ReturnDetails { get; } = new();

    public SellReturnViewModel(
        IDbConnectionFactory dbFactory,
        ISellRepository sellRepo,
        IPartRepository partRepo,
        IClientRepository clientRepo,
        IUserRepository userRepo,
        IBuyRepository buyRepo,
        ISerialNumberService snService)
    {
        _dbFactory = dbFactory;
        _sellRepo = sellRepo;
        _partRepo = partRepo;
        _clientRepo = clientRepo;
        _userRepo = userRepo;
        _buyRepo = buyRepo;
        _snService = snService;
    }

    /// <summary>
    /// 加载客户列表
    /// </summary>
    public async Task<List<ClientInfor>> LoadClientsAsync()
    {
        var clients = await _clientRepo.GetAllAsync();
        return clients.ToList();
    }

    /// <summary>
    /// 加载业务员列表
    /// </summary>
    public async Task<IEnumerable<UserInfor>> LoadUsersAsync()
    {
        return await _userRepo.GetAllAsync();
    }

    /// <summary>
    /// 加载源单据明细（客户已购买的配件）
    /// </summary>
    public async Task<List<dynamic>> LoadSourceDetailsAsync(string clientId, string? partNo, string? partName, string? cartype)
    {
        using var db = await _dbFactory.CreateAsync();
        var sql = @"SELECT d.sn, d.partid, d.partno, d.name,
                    d.amount, d.price, d.bill_price,
                    d.cartype, d.car_mark, d.memo, d.datetime,
                    d.unit, d.stotal, d.btotal, d.id,
                    d.place,
                    p.name_py, p.cartype_py,
                    ISNULL(r.returned_amount, 0) AS returned_amount,
                    d.amount - ISNULL(r.returned_amount, 0) AS remain_amount
                    FROM detail_sell d
                    INNER JOIN bill_sell b ON b.sn = d.sn
                    LEFT JOIN part_data p ON p.partid = d.partid
                    LEFT JOIN (
                        SELECT ret.tsn, ret.partid, SUM(ABS(ret.amount)) AS returned_amount
                        FROM detail_sell ret
                        INNER JOIN bill_sell bs ON bs.sn = ret.sn
                        WHERE ret.amount < 0 AND ISNULL(bs.flag,0) <> 3 AND bs.client = @ClientId
                        GROUP BY ret.tsn, ret.partid
                    ) r ON r.tsn = d.sn AND r.partid = d.partid
                    WHERE b.client = @ClientId
                      AND d.amount > 0
                      AND ISNULL(b.flag, 0) <> 3";

        var parameters = new DynamicParameters();
        parameters.Add("ClientId", clientId);

        if (!string.IsNullOrEmpty(partNo))
        {
            sql += " AND d.partno LIKE @PartNo";
            parameters.Add("PartNo", $"%{partNo}%");
        }
        if (!string.IsNullOrEmpty(partName))
        {
            sql += " AND (d.name LIKE @PartName OR p.name_py LIKE @PartNamePy)";
            parameters.Add("PartName", $"%{partName}%");
            parameters.Add("PartNamePy", $"%{partName}%");
        }
        if (!string.IsNullOrEmpty(cartype))
        {
            sql += " AND (d.cartype LIKE @CarType OR p.cartype_py LIKE @CarTypePy)";
            parameters.Add("CarType", $"%{cartype}%");
            parameters.Add("CarTypePy", $"%{cartype}%");

        }

        sql += " ORDER BY d.datetime DESC";

        return (await db.QueryAsync<dynamic>(sql, parameters)).ToList();
    }

    /// <summary>
    /// 加载退货单用于编辑
    /// </summary>
    public async Task<BillSell?> LoadBillForEditAsync(string sn)
    {
        return await _sellRepo.GetBySnAsync(sn);
    }

    /// <summary>
    /// 加载退货单明细
    /// </summary>
    public async Task<IEnumerable<DetailSell>> LoadDetailsAsync(string sn)
    {
        return await _sellRepo.GetDetailsAsync(sn);
    }

    /// <summary>
    /// 根据工号查找业务员名称
    /// </summary>
    public async Task<string?> GetWorkerNameAsync(string workid)
    {
        using var db = await _dbFactory.CreateAsync();
        return await db.QueryFirstOrDefaultAsync<string?>(
            "SELECT name FROM work_infor WHERE workid=@Workid", new { Workid = workid });
    }

    /// <summary>
    /// 根据业务员名称查找工号
    /// </summary>
    public async Task<string> ResolveWorkerIdFromNameAsync(string workerName)
    {
        using var db = await _dbFactory.CreateAsync();
        return await db.QueryFirstOrDefaultAsync<string>(
            "SELECT workid FROM work_infor WHERE name=@Name", new { Name = workerName }) ?? workerName;
    }

    /// <summary>
    /// 结算退货单
    /// </summary>
    public async Task SettleReturnAsync(
        string? editSn,
        bool isEditMode,
        BillSell bill,
        ObservableCollection<SellReturnItem> returnDetails,
        string? workerId)
    {
        string billNo;
        List<DetailSell>? originalDetails = null;

        if (isEditMode && !string.IsNullOrEmpty(editSn))
        {
            billNo = editSn;
            originalDetails = (await _sellRepo.GetDetailsAsync(billNo)).ToList();
            await AdjustReturnStockForEdit(originalDetails, returnDetails);
        }
        else
        {
            billNo = await _snService.GenerateSellReturnSN();
        }

        bill.Sn = billNo;

        if (isEditMode)
        {
            await _sellRepo.DeleteDetailsAsync(billNo);
            await _sellRepo.UpdateAsync(bill);
        }
        else
        {
            await _sellRepo.InsertBillAsync(bill);
        }

        // 批量查询配件进价（1次查询替代 N 次逐条查询）
        var partIds = returnDetails.Where(d => d.SourcePartId.HasValue).Select(d => d.SourcePartId!.Value).Distinct().ToList();
        var partMap = partIds.Count > 0 ? await _partRepo.GetByIdsAsync(partIds) : new Dictionary<long, PartData>();

        var detailList = new List<DetailSell>();

        foreach (var item in returnDetails)
        {
            if (item.ToWaste && item.SourcePartId.HasValue)
            {
                if (!string.IsNullOrEmpty(item.SourceBuySn))
                {
                    // 废品仓+关联进货记录 → 不入废品仓，直接退给供应商，库存不动
                }
                else
                {
                    // 纯废品仓（无进货关联）→ 入废品仓
                    if (!isEditMode)
                        await _partRepo.GetOrCreateWasteStockAsync(item.SourcePartId.Value, item.Amount);
                }
            }

            var detailMemo = item.Memo ?? "";
            if (item.ToWaste && !string.IsNullOrEmpty(item.SourceBuySn))
            {
                detailMemo = $"[BUY:{item.SourceBuySupplierSid}|{item.SourceBuySupplier}|{item.SourceBuyInPrice}|{item.SourceBuySn}]{detailMemo}";
            }

            PartData? partInfo = item.SourcePartId.HasValue && partMap.TryGetValue(item.SourcePartId.Value, out var pi) ? pi : null;
            var cb = partInfo != null && partInfo.Inprice > 0 ? partInfo.Inprice!.Value : 0m;

            // 从配件主档补全源单缺失的字段
            var unit = !string.IsNullOrEmpty(item.Unit) ? item.Unit : partInfo?.Unit;
            var place = item.ToWaste && string.IsNullOrEmpty(item.SourceBuySn)
                ? "废品仓"
                : (!string.IsNullOrEmpty(item.Place) ? item.Place : partInfo?.Place);

            detailList.Add(new DetailSell
            {
                Sn = billNo,
                Partid = item.SourcePartId,
                Partno = item.PartNo,
                Name = item.PartName,
                Cartype = item.Cartype,
                Unit = unit,
                Place = place,
                Amount = -item.Amount,
                Price = item.Price,
                BillPrice = item.Price,
                Stotal = -item.SubTotal,
                Btotal = -item.SubTotal,
                Tsn = item.SourceSn,
                Memo = detailMemo,
                Datetime = bill.Datetime,
                Flag = (int)BusinessConstants.BillFlag.Returned,
                Cb = cb,
                PartGg = partInfo?.PartGg,
                PartTh = partInfo?.PartTh,
                PartCclb = partInfo?.PartCclb,
                PartBzq = partInfo?.PartBzq,
                PartBzrq = partInfo?.PartBzrq
            });

            if (!isEditMode && item.SourcePartId.HasValue && !item.ToWaste)
            {
                await _partRepo.IncreaseStockAsync(item.SourcePartId.Value, item.Amount);
            }
        }
        await _sellRepo.InsertDetailsAsync(detailList);

        // 生成采购退货单
        await GenerateBuyReturnBillsAsync(billNo, bill.Datetime, returnDetails, workerId);
    }

    /// <summary>
    /// 生成采购退货单
    /// </summary>
    private async Task GenerateBuyReturnBillsAsync(string sellReturnSn, DateTime? billDate, ObservableCollection<SellReturnItem> returnDetails, string? workerId)
    {
        var buyLinkedItems = returnDetails
            .Where(d => d.ToWaste && !string.IsNullOrEmpty(d.SourceBuySn))
            .ToList();

        if (buyLinkedItems.Count == 0) return;

        try
        {
            var groupedBySupplier = buyLinkedItems.GroupBy(d => d.SourceBuySupplierSid ?? d.SourceBuySupplier);

            foreach (var group in groupedBySupplier)
            {
                // 每个供应商分组生成独立的采购退货单号，避免多供应商共用同一单号导致主键冲突
                var buyReturnSn = await _snService.GenerateBuyReturnSN();

                var supplierSid = group.Key;
                var firstItem = group.FirstOrDefault(i => !string.IsNullOrEmpty(i.SourceBuySupplierSid));
                var actualSupplierSid = firstItem?.SourceBuySupplierSid ?? supplierSid;

                var buyTotal = group.Sum(d => d.Amount * d.SourceBuyInPrice);

                var buyBill = new BillBuy
                {
                    Sn = buyReturnSn,
                    Supplier = actualSupplierSid,
                    Worker = workerId ?? "",
                    Operator = App.CurrentUser?.Username,
                    Datetime = billDate,
                    Total = -buyTotal,
                    BillTotal = -buyTotal,
                    Cash = 0,
                    Checks = 0,
                    Arrear = -buyTotal,
                    Zhifubao = 0,
                    Weixin = 0,
                    Yunfei = 0,
                    Flag = 2,
                    Memo = $"销退联动-销售退货单:{sellReturnSn}"
                };
                await _buyRepo.InsertBillAsync(buyBill);

                foreach (var item in group)
                {
                    await _buyRepo.InsertDetailAsync(new DetailBuy
                    {
                        Sn = buyReturnSn,
                        Partid = item.SourcePartId,
                        Partno = item.PartNo,
                        Name = item.PartName,
                        Cartype = item.Cartype,
                        Unit = item.Unit,
                        Amount = -item.Amount,
                        Inprice = item.SourceBuyInPrice,
                        Stotal = -(item.Amount * item.SourceBuyInPrice),
                        Tsn = item.SourceBuySn,
                        Datetime = billDate,
                        Memo = $"来自销售退货:{sellReturnSn}"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"生成采购退货单失败(不影响销售退货): {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 编辑退货单时调整库存差异
    /// </summary>
    private async Task AdjustReturnStockForEdit(List<DetailSell> originalDetails, ObservableCollection<SellReturnItem> returnDetails)
    {
        var newItems = new Dictionary<string, SellReturnItem>();
        foreach (var item in returnDetails)
        {
            var key = $"{item.SourceSn}_{item.SourcePartId}";
            if (newItems.ContainsKey(key))
            {
                newItems[key].Amount += item.Amount;
            }
            else
            {
                newItems[key] = new SellReturnItem
                {
                    SourceSn = item.SourceSn,
                    SourcePartId = item.SourcePartId,
                    Amount = item.Amount,
                    ToWaste = item.ToWaste,
                    SourceBuySn = item.SourceBuySn,
                    SourceBuyInPrice = item.SourceBuyInPrice
                };
            }
        }

        foreach (var orig in originalDetails)
        {
            if (!orig.Partid.HasValue) continue;
            var origAmount = (int)Math.Abs(orig.Amount ?? 0);
            var origIsWaste = !string.IsNullOrEmpty(orig.Place) && orig.Place.Trim() == "废品仓";
            var origHasBuyLink = !string.IsNullOrEmpty(orig.Memo) && orig.Memo.Contains("来自销售退货");
            var key = $"{orig.Tsn}_{orig.Partid}";

            if (newItems.TryGetValue(key, out var newItem))
            {
                var diff = newItem.Amount - origAmount;
                if (diff > 0)
                {
                    if (!string.IsNullOrEmpty(newItem.SourceBuySn))
                    {
                        // 关联了进货记录 → 库存不动
                    }
                    else if (newItem.ToWaste && newItem.SourcePartId.HasValue)
                        await _partRepo.GetOrCreateWasteStockAsync(newItem.SourcePartId.Value, diff);
                    else if (newItem.SourcePartId.HasValue)
                        await _partRepo.IncreaseStockAsync(newItem.SourcePartId.Value, diff);
                }
                else if (diff < 0)
                {
                    if (origIsWaste && !origHasBuyLink && orig.Partid.HasValue)
                        await _partRepo.DecreaseWasteStockAsync(orig.Partid.Value, Math.Abs(diff));
                    else if (!origIsWaste && orig.Partid.HasValue)
                        await _partRepo.DecreaseStockAsync(orig.Partid.Value, Math.Abs(diff));
                }
                newItems.Remove(key);
            }
            else
            {
                if (origIsWaste && !origHasBuyLink && orig.Partid.HasValue)
                    await _partRepo.DecreaseWasteStockAsync(orig.Partid.Value, origAmount);
                else if (orig.Partid.HasValue && !origIsWaste)
                    await _partRepo.DecreaseStockAsync(orig.Partid.Value, origAmount);
            }
        }

        foreach (var kvp in newItems)
        {
            var item = kvp.Value;
            if (!string.IsNullOrEmpty(item.SourceBuySn))
            {
                // 关联进货记录 → 库存不动
            }
            else if (item.ToWaste && item.SourcePartId.HasValue)
                await _partRepo.GetOrCreateWasteStockAsync(item.SourcePartId.Value, item.Amount);
            else if (item.SourcePartId.HasValue)
                await _partRepo.IncreaseStockAsync(item.SourcePartId.Value, item.Amount);
        }
    }
}
