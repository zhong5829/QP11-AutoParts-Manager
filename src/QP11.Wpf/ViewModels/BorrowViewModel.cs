using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Services;
using QP11.Wpf.Views;

namespace QP11.Wpf.ViewModels;

/// <summary>
/// 借货管理 ViewModel - 承担业务逻辑，与 UI 解耦
/// </summary>
public class BorrowViewModel : BaseViewModel
{
    private readonly IBuyRepository _buyRepo;
    private readonly IPartRepository _partRepo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly IUserRepository _userRepo;
    private readonly ISerialNumberService _snService;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IDbConnectionFactory _dbFactory;

    public ObservableCollection<BorrowDetailItem> Details { get; } = new();

    public BorrowViewModel(
        IBuyRepository buyRepo,
        IPartRepository partRepo,
        ISupplierRepository supplierRepo,
        IUserRepository userRepo,
        ISerialNumberService snService,
        IUnitOfWorkFactory uowFactory,
        IDbConnectionFactory dbFactory)
    {
        _buyRepo = buyRepo;
        _partRepo = partRepo;
        _supplierRepo = supplierRepo;
        _userRepo = userRepo;
        _snService = snService;
        _uowFactory = uowFactory;
        _dbFactory = dbFactory;
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
    /// 加载借货单列表
    /// </summary>
    public async Task<IEnumerable<BillBuy>> LoadBillListAsync()
    {
        return await _buyRepo.GetListAsync();
    }

    /// <summary>
    /// 根据单号加载借货单
    /// </summary>
    public async Task<BillBuy?> LoadBillAsync(string sn)
    {
        return await _buyRepo.GetBySnAsync(sn);
    }

    /// <summary>
    /// 加载借货单明细
    /// </summary>
    public async Task<IEnumerable<DetailBuy>> LoadDetailsAsync(string sn)
    {
        return await _buyRepo.GetDetailsAsync(sn);
    }

    /// <summary>
    /// 保存新借货单
    /// 借入 = 货进仓库 = 库存增加（IncreaseStock）。
    /// 在事务内原子完成"插单 + 插明细 + 加库存"。
    /// </summary>
    public async Task<string> SaveNewBillAsync(BillBuy bill, ObservableCollection<BorrowDetailItem> details)
    {
        var billNo = await _snService.GenerateBuySN();
        bill.Sn = billNo;

        using var uow = _uowFactory.Create();
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;
            var dbConn = uow.Connection;

            // 1. 插借货单主表
            await _buyRepo.InsertBillAsync(bill, txn);

            // 2. 插明细 + 逐条加库存（借入入库）
            var detailList = new List<DetailBuy>();
            foreach (var item in details)
            {
                detailList.Add(new DetailBuy
                {
                    Sn = billNo,
                    Partid = item.Partid,
                    Partno = item.PartNo,
                    Name = item.PartName,
                    Amount = (long?)item.Amount,
                    Inprice = item.Inprice,
                    Stotal = item.SubTotal
                });

                if (item.Partid.HasValue)
                {
                    await _partRepo.IncreaseStockAsync(item.Partid.Value, item.Amount, txn, dbConn);
                }
            }
            await _buyRepo.InsertDetailsAsync(detailList, txn);

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
    /// 更新借货单
    /// </summary>
    public async Task UpdateBillAsync(BillBuy bill)
    {
        await _buyRepo.UpdateAsync(bill);
    }

    /// <summary>
    /// 归还借货单：按原在借单(flag=3)生成一张 flag=4 负向还货单并扣减库存。
    /// 还货单与借货单结构对称——主表 total/明细 amount 取负，memo 记录原单号，明细 tsn 指向原单。
    /// 归还 = 货还回给供应商 = 库存减少（DecreaseStock）。在事务内原子完成"建还货单 + 扣减库存"。
    /// </summary>
    public async Task<string> SaveReturnAsync(BillBuy origBill)
    {
        if (origBill == null || string.IsNullOrEmpty(origBill.Sn))
            throw new ArgumentException("原借货单不能为空");

        if (origBill.Flag != 3)
            throw new InvalidOperationException("只能归还状态为'在借'(flag=3)的单据");

        // 加载原单明细
        var origDetails = (await _buyRepo.GetDetailsAsync(origBill.Sn)).ToList();
        if (origDetails.Count == 0)
            throw new InvalidOperationException("原借货单没有明细，无法归还");

        // 生成新还货单号
        var returnSn = await _snService.GenerateBuySN();

        using var uow = _uowFactory.Create();
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;
            var dbConn = uow.Connection;

            // 1. 写还货单主表：flag=4, total 取负, memo 记录原单号
            var returnBill = new BillBuy
            {
                Sn = returnSn,
                Supplier = origBill.Supplier,
                Worker = origBill.Worker,
                Operator = origBill.Operator,
                Total = -origBill.Total,
                Cash = 0,
                Flag = 4,
                Type = 0,
                Memo = $"还货单({origBill.Sn})",
                Datetime = DateTime.Now
            };
            await _buyRepo.InsertBillAsync(returnBill, txn);

            // 2. 写还货单明细：amount 取负, tsn 指向原单
            var returnDetails = new List<DetailBuy>();
            foreach (var d in origDetails)
            {
                returnDetails.Add(new DetailBuy
                {
                    Sn = returnSn,
                    Partid = d.Partid,
                    Partno = d.Partno,
                    Name = d.Name,
                    Amount = d.Amount.HasValue ? -d.Amount.Value : null,
                    Inprice = d.Inprice,
                    Stotal = d.Stotal.HasValue ? -d.Stotal.Value : null,
                    Tsn = origBill.Sn,
                    Type = 0
                });
            }
            await _buyRepo.InsertDetailsAsync(returnDetails, txn);

            // 3. 逐条扣减库存（归还 = 货还回给供应商 = 库存减少，按原 amount 绝对值）
            foreach (var d in origDetails)
            {
                if (d.Partid.HasValue && d.Amount.HasValue)
                {
                    await _partRepo.DecreaseStockAsync(d.Partid.Value, Math.Abs(d.Amount.Value), txn, dbConn);
                }
            }

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
