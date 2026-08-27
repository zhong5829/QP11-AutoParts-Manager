using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Core.Models;
using QP11.Wpf.Helpers;
using QP11.Wpf.Views;

namespace QP11.Wpf.ViewModels;

/// <summary>
/// 销售开单 ViewModel - 承担业务逻辑，与 UI 解耦
/// </summary>
public class SellViewModel : BaseViewModel
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly ISellRepository _sellRepo;
    private readonly IPartRepository _partRepo;
    private readonly IClientRepository _clientRepo;
    private readonly IUserRepository _userRepo;
    private readonly ISerialNumberService _snService;
    private readonly IArrearageRepository _arrearRepo;

    public ObservableCollection<SellControlItem> Details { get; } = new();

    private bool _isReturnMode;
    public bool IsReturnMode
    {
        get => _isReturnMode;
        set => SetProperty(ref _isReturnMode, value);
    }

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetProperty(ref _isEditMode, value);
    }

    private string? _editSn;
    public string? EditSn
    {
        get => _editSn;
        set => SetProperty(ref _editSn, value);
    }

    public SellViewModel(
        IDbConnectionFactory dbFactory,
        IUnitOfWorkFactory uowFactory,
        ISellRepository sellRepo,
        IPartRepository partRepo,
        IClientRepository clientRepo,
        IUserRepository userRepo,
        ISerialNumberService snService,
        IArrearageRepository arrearRepo)
    {
        _dbFactory = dbFactory;
        _uowFactory = uowFactory;
        _sellRepo = sellRepo;
        _partRepo = partRepo;
        _clientRepo = clientRepo;
        _userRepo = userRepo;
        _snService = snService;
        _arrearRepo = arrearRepo;
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
    /// 加载配件库存列表（简单查询）
    /// </summary>
    public async Task<IEnumerable<PartStockDisplay>> LoadPartListAsync(string? partNo, string? partName, string? cartype, string? className, int queryMode, bool hideScrapPlace)
    {
        string? partNamePy = partName != null && !ContainsChinese(partName) ? PinyinHelper.GetPinyinInitials(partName) : null;
        string? cartypePy = cartype != null && !ContainsChinese(cartype) ? PinyinHelper.GetPinyinInitials(cartype) : null;
        string? classPy = className != null && !ContainsChinese(className) ? PinyinHelper.GetPinyinInitials(className) : null;

        IEnumerable<PartStockDisplay> data;
        if (partNo == null && partName == null && cartype == null && className == null)
        {
            data = await _partRepo.GetStockListAsync(null, 200);
        }
        else
        {
            data = await _partRepo.GetStockListAdvancedAsync(partNo, partName, partNamePy, cartype, cartypePy, className, classPy, queryMode);
        }

        var list = data.ToList();
        var result = hideScrapPlace ? list.Where(p => p.Place != "废品仓").ToList() : list;
        return result;
    }

    /// <summary>
    /// 按多个配件编号加载库存列表（多条件查询弹窗用）
    /// </summary>
    public async Task<IEnumerable<PartStockDisplay>> LoadPartListByCodesAsync(IEnumerable<string> codes, bool hideScrapPlace)
    {
        var data = await _partRepo.GetStockListByCodesAsync(codes);
        var list = data.ToList();
        var result = hideScrapPlace ? list.Where(p => p.Place != "废品仓").ToList() : list;
        return result;
    }

    private static bool ContainsChinese(string text)
    {
        return text.Any(c => c >= 0x4e00 && c <= 0x9fff);
    }

    /// <summary>
    /// 加载单据用于编辑
    /// </summary>
    public async Task<BillSell?> LoadBillForEditAsync(string sn)
    {
        return await _sellRepo.GetBySnAsync(sn);
    }

    /// <summary>
    /// 加载单据明细
    /// </summary>
    public async Task<IEnumerable<DetailSell>> LoadDetailsAsync(string sn)
    {
        return await _sellRepo.GetDetailsAsync(sn);
    }

    /// <summary>
    /// 保存销售单 - 核心业务逻辑（事务保护）
    /// </summary>
    public async Task<SaveBillResult> SaveBillAsync(
        string? editSn,
        bool isEditMode,
        bool isReturnMode,
        string clientId,
        DateTime? billDate,
        string? workerId,
        string? operatorName,
        decimal discountRate,
        decimal cash,
        decimal weixin,
        decimal zhifubao,
        decimal checks,
        string? memo,
        string? checkno)
    {
        var totalAmount = Details.Sum(d => d.Price * d.Amount);
        var billTotal = discountRate > 0 ? Math.Round(totalAmount * discountRate, 2) : totalAmount;
        var totalPaid = cash + weixin + zhifubao + checks;
        var arrear = Math.Max(0, billTotal - totalPaid);

        bool isEditing = isEditMode && !string.IsNullOrEmpty(editSn);

        // 事务前：预查配件进价和编辑模式的原始明细
        var detailPartIds = Details.Select(d => d.Partid).Where(p => p.HasValue).Select(p => p!.Value).Distinct().ToList();
        var partMap = await _partRepo.GetByIdsAsync(detailPartIds);
        var detailCostMap = new Dictionary<long, decimal>();
        foreach (var kvp in partMap)
        {
            if (kvp.Value.Inprice > 0)
                detailCostMap[kvp.Key] = kvp.Value.Inprice!.Value;
        }

        List<DetailSell>? originalDetails = null;
        if (isEditing)
        {
            originalDetails = (await _sellRepo.GetDetailsAsync(editSn!)).ToList();
        }

        // 构建明细列表
        var detailList = new List<DetailSell>();
        foreach (var item in Details)
        {
            var cb = item.Partid.HasValue && detailCostMap.TryGetValue(item.Partid.Value, out var cost) ? cost : 0m;
            var partInfo = item.Partid.HasValue && partMap.TryGetValue(item.Partid.Value, out var pd) ? pd : null;

            detailList.Add(new DetailSell
            {
                Partid = item.Partid,
                Partno = item.PartNo,
                Name = item.PartName,
                Cartype = item.Cartype,
                CarMark = item.CarMark,
                Unit = partInfo?.Unit,
                Place = !string.IsNullOrEmpty(item.Place) ? item.Place : partInfo?.Place,
                Amount = (long)item.Amount,
                Price = item.Price,
                BillPrice = item.BillPrice,
                Stotal = item.SubTotal,
                Btotal = Math.Round(item.BillPrice * item.Amount, 2),
                Cb = cb,
                Memo = item.Memo,
                Datetime = billDate,
                Flag = isReturnMode ? (int)BusinessConstants.BillFlag.Returned : (int)BusinessConstants.BillFlag.Confirmed,
                PartGg = partInfo?.PartGg,
                PartTh = partInfo?.PartTh,
                PartCclb = partInfo?.PartCclb,
                PartBzq = partInfo?.PartBzq,
                PartBzrq = partInfo?.PartBzrq
            });
        }

        string billNo;
        using var uow = _uowFactory.Create();
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;
            var dbConn = uow.Connection;

            if (isEditing)
            {
                billNo = editSn!;

                // 编辑模式：先回补原始库存（全部回补，再扣新明细）
                foreach (var orig in originalDetails!)
                {
                    if (!orig.Partid.HasValue) continue;
                    var origAmount = orig.Amount ?? 0m;
                    // 原始是销售单 → 回补库存（Increase）；原始是退货单 → 扣减库存（Decrease）
                    var origIsReturn = orig.Flag == (int)BusinessConstants.BillFlag.Returned;
                    if (origIsReturn)
                        await _partRepo.DecreaseStockAsync(orig.Partid.Value, Math.Abs(origAmount), txn, dbConn);
                    else
                        await _partRepo.IncreaseStockAsync(orig.Partid.Value, Math.Abs(origAmount), txn, dbConn);
                }

                // 删除旧明细、更新单头
                await _sellRepo.DeleteDetailsAsync(billNo, txn);
                await _sellRepo.UpdateAsync(new BillSell
                {
                    Sn = billNo,
                    Client = clientId,
                    Worker = workerId,
                    Operator = operatorName,
                    Datetime = billDate,
                    Total = totalAmount,
                    BillTotal = billTotal,
                    DiscountRate = discountRate,
                    TotalPayment = discountRate > 0 ? Math.Round(totalAmount * discountRate, 2) : totalAmount,
                    BillPayment = billTotal,
                    Cash = cash,
                    Collection = 0,
                    Weixin = weixin,
                    Zhifubao = zhifubao,
                    Checks = checks,
                    Arrear = arrear,
                    Yunfei = 0,
                    Flag = isReturnMode ? (int)BusinessConstants.BillFlag.Returned : (int)BusinessConstants.BillFlag.Confirmed,
                    Memo = memo?.Trim(),
                    Checkno = checkno?.Trim()
                }, txn);
            }
            else
            {
                billNo = await _snService.GenerateSellSN(txn);
                var bill = new BillSell
                {
                    Sn = billNo,
                    Client = clientId,
                    Worker = workerId,
                    Operator = operatorName,
                    Datetime = billDate,
                    Total = totalAmount,
                    BillTotal = billTotal,
                    DiscountRate = discountRate,
                    TotalPayment = discountRate > 0 ? Math.Round(totalAmount * discountRate, 2) : totalAmount,
                    BillPayment = billTotal,
                    Cash = cash,
                    Collection = 0,
                    Weixin = weixin,
                    Zhifubao = zhifubao,
                    Checks = checks,
                    Arrear = arrear,
                    Yunfei = 0,
                    Flag = isReturnMode ? (int)BusinessConstants.BillFlag.Returned : (int)BusinessConstants.BillFlag.Confirmed,
                    Memo = memo?.Trim(),
                    Checkno = checkno?.Trim()
                };
                await _sellRepo.InsertBillAsync(bill, txn);
            }

            // 赋值 SN 并插入新明细
            foreach (var d in detailList) d.Sn = billNo;
            await _sellRepo.InsertDetailsAsync(detailList, txn);

            // 扣减新明细库存
            foreach (var item in Details)
            {
                if (!item.Partid.HasValue) continue;
                if (isReturnMode)
                    await _partRepo.IncreaseStockAsync(item.Partid.Value, Math.Abs(item.Amount), txn, dbConn);
                else
                {
                    var affected = await _partRepo.DecreaseStockAsync(item.Partid.Value, item.Amount, txn, dbConn);
                    if (affected == 0)
                        throw new InvalidOperationException($"配件[{item.PartName}]库存不足，无法保存。请先补货或减少数量。");
                }
            }

            // 欠款记录：编辑模式下先删除旧记录，再按需插入新记录
            if (isEditing)
            {
                await _arrearRepo.DeleteBySnAsync(billNo, txn);
            }
            if (arrear > 0.01m)
            {
                await _arrearRepo.InsertAsync(new Arrearage
                {
                    Bid = clientId,
                    Type = BusinessConstants.ArrearType.Sell,
                    Btype = BusinessConstants.ArrearBtype.Sell,
                    Total = arrear,
                    Sn = billNo
                }, txn);
            }

            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }

        return new SaveBillResult
        {
            Success = true,
            BillNo = billNo,
            BillTotal = billTotal,
            TotalPaid = totalPaid,
            Arrear = arrear
        };
    }

    /// <summary>
    /// 作废单据 - 回补库存并更新状态（事务保护）
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

            await _sellRepo.UpdateBillStatusAsync(sn, (int)BusinessConstants.BillFlag.Voided, txn);

            foreach (var d in details)
            {
                if (!d.Partid.HasValue) continue;
                var amount = d.Amount ?? 0m;
                if (amount == 0) continue;
                // 销售单作废 → 回补库存；退货单作废 → 扣减库存
                var isReturn = d.Flag == (int)BusinessConstants.BillFlag.Returned;
                if (isReturn)
                    await _partRepo.DecreaseStockAsync(d.Partid.Value, Math.Abs(amount), txn, dbConn);
                else
                    await _partRepo.IncreaseStockAsync(d.Partid.Value, Math.Abs(amount), txn, dbConn);
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
    /// 结算单据
    /// </summary>
    public async Task SettleBillAsync(string sn)
    {
        await _sellRepo.UpdateBillStatusAsync(sn, (int)BusinessConstants.BillFlag.Confirmed);
    }

    /// <summary>
    /// 验证客户ID - 根据客户名称查找
    /// </summary>
    public async Task<string?> ResolveClientIdAsync(string clientText)
    {
        using var db = await _dbFactory.CreateAsync();
        return await Dapper.SqlMapper.QueryFirstOrDefaultAsync<string?>(
            db, "SELECT cid FROM client_infor WHERE name=@Name OR cid=@Name",
            new { Name = clientText });
    }

    /// <summary>
    /// 根据业务员名称查找工号
    /// </summary>
    public async Task<string> ResolveWorkerIdAsync(string workerName)
    {
        using var db = await _dbFactory.CreateAsync();
        return await Dapper.SqlMapper.QueryFirstOrDefaultAsync<string>(
            db, "SELECT workid FROM work_infor WHERE name=@Name", new { Name = workerName }) ?? workerName;
    }

    /// <summary>
    /// 根据工号查找业务员名称
    /// </summary>
    public async Task<string?> GetWorkerNameAsync(string workid)
    {
        using var db = await _dbFactory.CreateAsync();
        return await Dapper.SqlMapper.QueryFirstOrDefaultAsync<string?>(
            db, "SELECT name FROM work_infor WHERE workid=@Workid", new { Workid = workid });
    }

    /// <summary>
    /// 查询单据列表
    /// </summary>
    public async Task<IEnumerable<BillSellDisplay>> SearchBillsAsync(DateTime? startDate, DateTime? endDate, string? clientKeyword)
    {
        using var db = await _dbFactory.CreateAsync();
        var sql = @"SELECT bill_sell.sn AS Sn, bill_sell.datetime AS Datetime,
                bill_sell.client AS Client, bill_sell.worker AS Worker,
                bill_sell.total AS Total, bill_sell.bill_total AS BillTotal, bill_sell.flag AS Flag,
                bill_sell.memo AS Memo,
                client_infor.name AS ClientName,
                ISNULL(work_infor.name, bill_sell.worker) AS WorkerName
                FROM bill_sell
                LEFT JOIN client_infor ON client_infor.cid = bill_sell.client
                LEFT JOIN work_infor ON work_infor.workid = bill_sell.worker
                WHERE 1=1";
        if (startDate.HasValue) sql += " AND bill_sell.datetime >= @Start";
        if (endDate.HasValue) sql += " AND bill_sell.datetime < DATEADD(day, 1, @End)";
        if (!string.IsNullOrWhiteSpace(clientKeyword))
            sql += " AND (client_infor.name LIKE @Client OR bill_sell.client LIKE @Client)";
        sql += " ORDER BY bill_sell.sn DESC";

        var data = (await Dapper.SqlMapper.QueryAsync(db, sql, new
        {
            Start = startDate,
            End = endDate,
            Client = $"%{clientKeyword?.Trim()}%"
        })).ToList();

        return data.Select(b => new BillSellDisplay
        {
            Sn = (string?)b.Sn,
            Datetime = (DateTime?)b.Datetime,
            Client = (string?)b.ClientName ?? (string?)b.Client,
            Worker = (string?)b.WorkerName ?? (string?)b.Worker,
            Total = (decimal?)b.Total,
            BillTotal = (decimal?)b.BillTotal,
            Flag = b.Flag == null ? null : Convert.ToInt32(Convert.ToDecimal(b.Flag)),
            Memo = (string?)b.Memo
        }).ToList();
    }
}

/// <summary>
/// 保存单据结果
/// </summary>
public class SaveBillResult
{
    public bool Success { get; set; }
    public string BillNo { get; set; } = "";
    public decimal BillTotal { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal Arrear { get; set; }
    public string? Error { get; set; }

    public static SaveBillResult Fail(string error) => new() { Success = false, Error = error };
}
