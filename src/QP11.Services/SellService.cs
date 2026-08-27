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

public class SellService : ISellService
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ISellRepository _sellRepo;
    private readonly IPartRepository _partRepo;
    private readonly IArrearageRepository _arrearRepo;
    private readonly IMemberCardRepository _memberRepo;
    private readonly IValidationService _validator;
    private readonly ISerialNumberService _snService;

    public SellService(IDbConnectionFactory dbFactory, ISellRepository sellRepo, IPartRepository partRepo, IArrearageRepository arrearRepo, IMemberCardRepository memberRepo, IValidationService validator, ISerialNumberService snService)
    {
        _dbFactory = dbFactory;
        _sellRepo = sellRepo;
        _partRepo = partRepo;
        _arrearRepo = arrearRepo;
        _memberRepo = memberRepo;
        _validator = validator;
        _snService = snService;
    }

    public async Task<string> CreateSellOrderAsync(BillSell bill, List<DetailSell> details, decimal cash, decimal weixin, decimal zhifubao, decimal memberPay, string? memberCardNo = null)
    {
        _validator.ValidateRequired(bill.Client!, "客户");
        if (details.Count == 0) throw new BusinessRuleException("销售明细不能为空");

        var totalAmount = details.Sum(d => (d.Price ?? 0m) * (d.Amount ?? 0));
        var discountRate = bill.DiscountRate ?? 1m;
        _validator.ValidateDiscountRate(discountRate);
        var billTotal = Math.Round(totalAmount * discountRate, 2);

        foreach (var d in details)
        {
            if (d.Partid.HasValue)
                await _validator.ValidateStockAsync(d.Partid.Value, d.Amount ?? 0);
        }

        var arrear = billTotal - (cash + weixin + zhifubao + memberPay);
        if (arrear > 0.01m)
            await _validator.ValidateClientCreditAsync(bill.Client!, arrear);

        using var uow = new UnitOfWork(_dbFactory);
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;
            var dbConn = uow.Connection;

            var billNo = await _snService.GenerateSellSN(txn);
            bill.Sn = billNo;
            bill.Total = totalAmount;
            bill.BillTotal = billTotal;
            bill.Cash = cash;
            bill.Weixin = weixin;
            bill.Zhifubao = zhifubao;
            // 尊重调用方设置的Flag（Confirmed/Returned等），未设置时默认Draft
            bill.Flag ??= (int)BusinessConstants.BillFlag.Draft;

            await _sellRepo.InsertBillAsync(bill, txn);

            foreach (var d in details)
            {
                d.Sn = billNo;
                d.Stotal = Math.Round((d.Price ?? 0m) * (d.Amount ?? 0) * (d.DiscountRate ?? 1m), 2);
                // 明细flag跟随单头flag（销售=1, 退货=2）
                d.Flag ??= bill.Flag;
            }
            await _sellRepo.InsertDetailsAsync(details, txn);

            foreach (var d in details)
            {
                if (!d.Partid.HasValue) continue;
                // 退货单 → 库存增加；销售单 → 库存扣减
                var isReturn = d.Flag == (int)BusinessConstants.BillFlag.Returned;
                if (isReturn)
                    await _partRepo.IncreaseStockAsync(d.Partid.Value, d.Amount ?? 0, txn, dbConn);
                else
                    await _partRepo.DecreaseStockAsync(d.Partid.Value, d.Amount ?? 0, txn, dbConn);
            }

            if (arrear > 0.01m)
            {
                await _arrearRepo.InsertAsync(new Arrearage
                {
                    Bid = bill.Client,
                    Type = BusinessConstants.ArrearType.Sell,
                    Btype = BusinessConstants.ArrearBtype.Sell,
                    Total = arrear,
                    Sn = billNo
                }, txn);
            }

            if (!string.IsNullOrEmpty(memberCardNo) && memberPay > 0)
            {
                await _memberRepo.ConsumeAsync(memberCardNo, memberPay, txn);
            }

            await uow.CommitAsync();
            return billNo;
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    public async Task VoidSellOrderAsync(string sn, List<DetailSell> details)
    {
        using var uow = new UnitOfWork(_dbFactory);
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

            // 删除对应欠款记录（与WPF端 SellViewModel 一致，在同一事务内执行）
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
