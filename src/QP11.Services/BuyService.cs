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

public class BuyService : IBuyService
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IBuyRepository _buyRepo;
    private readonly IPartRepository _partRepo;
    private readonly IArrearageRepository _arrearRepo;
    private readonly IValidationService _validator;
    private readonly ISerialNumberService _snService;

    public BuyService(IDbConnectionFactory dbFactory, IBuyRepository buyRepo, IPartRepository partRepo, IArrearageRepository arrearRepo, IValidationService validator, ISerialNumberService snService)
    {
        _dbFactory = dbFactory;
        _buyRepo = buyRepo;
        _partRepo = partRepo;
        _arrearRepo = arrearRepo;
        _validator = validator;
        _snService = snService;
    }

    public async Task<string> CreateBuyOrderAsync(BillBuy bill, List<DetailBuy> details, decimal credit = 0)
    {
        _validator.ValidateRequired(bill.Supplier!, "供应商");
        if (details.Count == 0) throw new BusinessRuleException("采购明细不能为空");
        if (details.Any(d => (d.Amount ?? 0) <= 0)) throw new BusinessRuleException("采购明细数量必须大于 0");

        var totalAmount = details.Sum(d => (d.Inprice ?? 0m) * (d.Amount ?? 0));

        using var uow = new UnitOfWork(_dbFactory);
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;

            var billNo = await _snService.GenerateBuySN(txn);

            bill.Sn = billNo;
            bill.Total = totalAmount;
            bill.BillTotal = totalAmount;
            bill.Flag = (int)BusinessConstants.BillFlag.Draft;

            await _buyRepo.InsertBillAsync(bill, txn);

            foreach (var d in details)
            {
                d.Sn = billNo;
                d.Stotal = Math.Round((d.Inprice ?? 0m) * (d.Amount ?? 0), 2);
            }
            await _buyRepo.InsertDetailsAsync(details, txn);

            if (credit > 0.01m)
            {
                await _arrearRepo.InsertAsync(new Arrearage
                {
                    Bid = bill.Supplier,
                    Type = 1,
                    Btype = 1,
                    Total = credit,
                    Sn = billNo
                }, txn);
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

    public async Task ConfirmStockInAsync(string sn, List<DetailBuy> details)
    {
        if (details.Any(d => (d.Amount ?? 0) <= 0)) throw new BusinessRuleException("采购明细数量必须大于 0");

        using var uow = new UnitOfWork(_dbFactory);
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;
            var dbConn = uow.Connection;

            var existingBill = await _buyRepo.GetBySnAsync(sn, txn);
            if (existingBill != null && existingBill.Flag == (int)BusinessConstants.BillFlag.Confirmed)
                throw new BusinessRuleException("该采购单已确认入库，不可重复操作");

            await _buyRepo.UpdateBillStatusAsync(sn, (int)BusinessConstants.BillFlag.Confirmed, txn);
            foreach (var d in details)
            {
                await _partRepo.IncreaseStockAsync(d.Partid ?? 0, d.Amount ?? 0, txn, dbConn);
            }

            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    public async Task<string> CreateBuyReturnAsync(string supplierId, string? supplierName, List<BuyReturnDetail> returnDetails)
    {
        if (returnDetails.Count == 0) throw new BusinessRuleException("退货明细不能为空");
        _validator.ValidateRequired(supplierId, "供应商");

        var totalReturn = returnDetails.Sum(d => d.InPrice * d.ReturnAmount);

        using var uow = new UnitOfWork(_dbFactory);
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;

            var returnSn = await _snService.GenerateBuyReturnSN(txn);

            var bill = new BillBuy
            {
                Sn = returnSn,
                Supplier = supplierId,
                Worker = "",
                Operator = "",
                Total = -totalReturn,
                BillTotal = -totalReturn,
                Cash = 0,
                Checks = 0,
                Arrear = -totalReturn,
                Zhifubao = 0,
                Weixin = 0,
                Yunfei = 0,
                Flag = 2,
                Memo = $"采购退货-供应商:{supplierName}"
            };
            await _buyRepo.InsertBillAsync(bill, txn);

            // 批量查配件主档补全字段
            var partIds = returnDetails.Where(d => d.PartId.HasValue).Select(d => d.PartId!.Value).Distinct().ToList();
            var partMap = partIds.Count > 0 ? await _partRepo.GetByIdsAsync(partIds) : new Dictionary<long, PartData>();

            var detailList = new List<DetailBuy>();
            foreach (var item in returnDetails)
            {
                var partInfo = item.PartId.HasValue && partMap.TryGetValue(item.PartId.Value, out var pd) ? pd : null;

                detailList.Add(new DetailBuy
                {
                    Sn = returnSn,
                    Partid = item.PartId,
                    Partno = item.PartNo,
                    Name = item.PartName,
                    Amount = -item.ReturnAmount,
                    Unit = partInfo?.Unit ?? "",
                    Carname = partInfo?.Carname ?? "",
                    Cartype = !string.IsNullOrEmpty(item.Cartype) ? item.Cartype : partInfo?.Cartype ?? "",
                    Inprice = item.InPrice,
                    Stotal = -(item.ReturnAmount * item.InPrice),
                    Pfprice = partInfo?.Pfprice,
                    Lsprice = partInfo?.Lsprice,
                    Place = partInfo?.Place ?? "",
                    Class = partInfo?.ClassName,
                    Memo = $"来自进货单:{item.SourceSn}",
                    Tsn = item.SourceSn,
                    PartGg = partInfo?.PartGg,
                    PartTh = partInfo?.PartTh,
                    PartCclb = partInfo?.PartCclb,
                    PartBzq = partInfo?.PartBzq,
                    PartBzrq = partInfo?.PartBzrq
                });

                if (item.PartId.HasValue)
                    await _partRepo.DecreaseStockAsync(item.PartId.Value, item.ReturnAmount, txn, uow.Connection);
            }

            await _buyRepo.InsertDetailsAsync(detailList, txn);

            await uow.CommitAsync();
            return returnSn;
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }
}
