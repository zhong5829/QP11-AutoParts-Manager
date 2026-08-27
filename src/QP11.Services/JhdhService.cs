using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Exceptions;
using QP11.Core.Interfaces;
using QP11.Data.Infrastructure;

namespace QP11.Services;

/// <summary>
/// 计划订货服务 — 创建计划单、转采购入库、作废
/// </summary>
public class JhdhService : IJhdhService
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IJhdhRepository _jhdhRepo;
    private readonly IBuyRepository _buyRepo;
    private readonly IPartRepository _partRepo;
    private readonly IArrearageRepository _arrearRepo;
    private readonly IValidationService _validator;
    private readonly ISerialNumberService _snService;

    public JhdhService(
        IDbConnectionFactory dbFactory,
        IJhdhRepository jhdhRepo,
        IBuyRepository buyRepo,
        IPartRepository partRepo,
        IArrearageRepository arrearRepo,
        IValidationService validator,
        ISerialNumberService snService)
    {
        _dbFactory = dbFactory;
        _jhdhRepo = jhdhRepo;
        _buyRepo = buyRepo;
        _partRepo = partRepo;
        _arrearRepo = arrearRepo;
        _validator = validator;
        _snService = snService;
    }

    /// <summary>
    /// 创建计划订货单（事务内：生成单号 + 插单头 + 插明细）
    /// </summary>
    public async Task<string> CreatePlanOrderAsync(BillJhdh bill, List<DetailJhdh> details)
    {
        _validator.ValidateRequired(bill.Supplier!, "供应商");
        if (details.Count == 0) throw new BusinessRuleException("计划明细不能为空");

        var totalAmount = details.Sum(d => (d.Price ?? 0m) * (d.Amount ?? 0));

        using var uow = new UnitOfWork(_dbFactory);
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;

            var billNo = await _snService.GeneratePlanSN(txn);

            bill.Sn = billNo;
            bill.Total = totalAmount;
            bill.Flag = 0; // bill_jhdh: 0=待处理

            await _jhdhRepo.InsertBillAsync(bill, txn);

            foreach (var d in details)
            {
                d.Sn = billNo;
                d.Total = Math.Round((d.Price ?? 0m) * (d.Amount ?? 0), 2);
                d.Datetime = bill.Datetime;
            }
            await _jhdhRepo.InsertDetailsAsync(details, txn);

            await uow.CommitAsync();
            return billNo;
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 更新计划订货单（事务内：更新单头 + 删旧明细 + 插新明细）
    /// </summary>
    public async Task UpdatePlanOrderAsync(BillJhdh bill, List<DetailJhdh> details)
    {
        _validator.ValidateRequired(bill.Supplier!, "供应商");
        if (details.Count == 0) throw new BusinessRuleException("计划明细不能为空");

        var totalAmount = details.Sum(d => (d.Price ?? 0m) * (d.Amount ?? 0));
        bill.Total = totalAmount;

        using var uow = new UnitOfWork(_dbFactory);
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;

            await _jhdhRepo.UpdateAsync(bill, txn);
            await _jhdhRepo.DeleteDetailsBySnAsync(bill.Sn!, txn);

            foreach (var d in details)
            {
                d.Sn = bill.Sn;
                d.Total = Math.Round((d.Price ?? 0m) * (d.Amount ?? 0), 2);
                d.Datetime = bill.Datetime;
            }
            await _jhdhRepo.InsertDetailsAsync(details, txn);

            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 将计划单转为采购入库单（核心流程）
    /// 事务内完成：
    /// 1. 校验计划单状态
    /// 2. 生成 bill_buy + detail_buy
    /// 3. 增加库存
    /// 4. 记录欠款
    /// 5. 更新 jhdh flag = 1（已执行）
    /// </summary>
    public async Task<string> ConvertToBuyOrderAsync(string jhdhSn, List<DetailBuy> buyDetails, decimal cash = 0, decimal credit = 0)
    {
        using var uow = new UnitOfWork(_dbFactory);
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;
            var dbConn = uow.Connection;

            // 1. 校验计划单
            var jhdhBill = await _jhdhRepo.GetBySnAsync(jhdhSn, txn);
            if (jhdhBill == null) throw new BusinessRuleException($"计划单 {jhdhSn} 不存在");
            if (jhdhBill.Flag == 1) // jhdh: 1=已执行
                throw new BusinessRuleException("该计划单已执行转采购，不可重复操作");
            if (jhdhBill.Flag == 2) // jhdh: 2=已作废
                throw new BusinessRuleException("该计划单已作废，无法转采购");

            // 2. 生成采购单号
            var buySn = await _snService.GenerateBuySN(txn);

            var totalAmount = buyDetails.Sum(d => (d.Inprice ?? 0m) * (d.Amount ?? 0));

            // 3. 插入 bill_buy
            var buyBill = new BillBuy
            {
                Sn = buySn,
                Supplier = jhdhBill.Supplier,
                Worker = jhdhBill.Worker,
                Operator = jhdhBill.Operator,
                Datetime = jhdhBill.Datetime,
                Total = totalAmount,
                BillTotal = totalAmount,
                Cash = cash,
                Checks = 0,
                Arrear = credit,
                Zhifubao = 0,
                Weixin = 0,
                Yunfei = 0,
                Flag = (int)BusinessConstants.BillFlag.Confirmed,
                Memo = $"由计划单{jhdhSn}转入"
            };
            await _buyRepo.InsertBillAsync(buyBill, txn);

            // 4. 插入 detail_buy
            foreach (var d in buyDetails)
            {
                d.Sn = buySn;
                d.Stotal = Math.Round((d.Inprice ?? 0m) * (d.Amount ?? 0), 2);
                d.Datetime = jhdhBill.Datetime;
            }
            await _buyRepo.InsertDetailsAsync(buyDetails, txn);

            // 5. 增加库存
            foreach (var d in buyDetails)
            {
                if (d.Partid.HasValue && d.Partid.Value > 0 && d.Amount.HasValue && d.Amount.Value > 0)
                    await _partRepo.IncreaseStockAsync(d.Partid.Value, d.Amount.Value, txn, dbConn);
            }

            // 6. 记录欠款
            if (credit > 0.01m)
            {
                await _arrearRepo.InsertAsync(new Arrearage
                {
                    Bid = jhdhBill.Supplier,
                    Type = 1,
                    Btype = 1,
                    Total = credit,
                    Sn = buySn
                }, txn);
            }

            // 7. 更新计划单状态为已执行 (jhdh: flag=1)
            await _jhdhRepo.UpdateBillStatusAsync(jhdhSn, 1, txn);

            await uow.CommitAsync();
            return buySn;
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 作废计划单（jhdh: flag = 2）
    /// </summary>
    public async Task CancelPlanOrderAsync(string sn)
    {
        var bill = await _jhdhRepo.GetBySnAsync(sn);
        if (bill == null) throw new BusinessRuleException($"计划单 {sn} 不存在");
        if (bill.Flag == 1) // jhdh: 1=已执行
            throw new BusinessRuleException("已执行的计划单不能作废");
        if (bill.Flag == 2) // jhdh: 2=已作废
            throw new BusinessRuleException("该计划单已作废");

        await _jhdhRepo.UpdateBillStatusAsync(sn, 2); // jhdh: flag=2=已作废
    }
}
