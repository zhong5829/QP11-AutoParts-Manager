using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Data.Infrastructure;

namespace QP11.Services;

public class FinanceService : IFinanceService
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IAccountRepository _accountRepo;
    private readonly IArrearageRepository _arrearRepo;
    private readonly IPaysRepository _paysRepo;

    public FinanceService(IDbConnectionFactory dbFactory, IAccountRepository accountRepo, IArrearageRepository arrearRepo, IPaysRepository paysRepo)
    {
        _dbFactory = dbFactory;
        _accountRepo = accountRepo;
        _arrearRepo = arrearRepo;
        _paysRepo = paysRepo;
    }

    public async Task<int> ReceivePaymentAsync(long accountId, decimal amount, string sn, string memo = "")
    {
        using var uow = new UnitOfWork(_dbFactory);
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;

            await _accountRepo.UpdateBalanceAsync(accountId, amount, txn);
            var result = await _paysRepo.InsertAsync(new Pays
            {
                Sn = sn,
                Je = amount,
                Flag = 1,
                Btype = 1
            }, txn);

            await uow.CommitAsync();
            return result;
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    public async Task<int> PaySupplierAsync(long accountId, decimal amount, string sn, string memo = "")
    {
        using var uow = new UnitOfWork(_dbFactory);
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;

            await _accountRepo.UpdateBalanceAsync(accountId, -amount, txn);
            var result = await _paysRepo.InsertAsync(new Pays
            {
                Sn = sn,
                Je = -amount,
                Flag = 1,
                Btype = 2
            }, txn);

            await uow.CommitAsync();
            return result;
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    public async Task<decimal> GetClientArrearTotalAsync(string clientId)
    {
        return await _arrearRepo.GetClientArrearTotalAsync(clientId);
    }

    /// <summary>
    /// 批量确认欠款到账：事务内更新arrearage.charge + 关联单据挂账 + 收支记录
    /// </summary>
    public async Task ConfirmArrearagePaymentAsync(decimal totalAmount, IEnumerable<(long Id, decimal Amount)> payments, string payMethod, string memo = "")
    {
        using var uow = new UnitOfWork(_dbFactory);
        try
        {
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;

            // 1. 逐条更新欠款记录charge + 关联单据挂账 + 付款方式字段
            foreach (var (id, amount) in payments)
            {
                await _arrearRepo.UpdatePaymentAsync(id, amount, payMethod, txn);
            }

            // 2. 写入收支记录
            await _paysRepo.InsertAsync(new Pays
            {
                Je = totalAmount,
                Flag = 1,
                Btype = 1
            }, txn);

            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }
}
