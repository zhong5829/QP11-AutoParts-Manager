using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Interfaces;

namespace QP11.Services;

public class SerialNumberService : ISerialNumberService
{
    private readonly IDbConnectionFactory _dbFactory;

    public SerialNumberService(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<string> GenerateSellSN(IDbTransaction? transaction = null) => await GenerateSN(2, 8, transaction);
    public async Task<string> GenerateBuySN(IDbTransaction? transaction = null) => await GenerateSN(1, 8, transaction);
    public async Task<string> GenerateSellReturnSN(IDbTransaction? transaction = null) => await GenerateSN(2, 8, transaction);
    public async Task<string> GenerateBuyReturnSN(IDbTransaction? transaction = null) => await GenerateSN(1, 8, transaction);
    public async Task<string> GenerateSupplierSN(IDbTransaction? transaction = null) => await GenerateSN(3, 5, transaction);
    public async Task<string> GenerateWorkerSN(IDbTransaction? transaction = null) => await GenerateSN(4, 5, transaction);
    public async Task<string> GenerateClientSN(IDbTransaction? transaction = null) => await GenerateSN(5, 5, transaction);
    public async Task<string> GenerateShopSN(IDbTransaction? transaction = null) => await GenerateSN(6, 8, transaction);
    public async Task<string> GeneratePlanSN(IDbTransaction? transaction = null) => await GenerateSN(7, 8, transaction);
    public async Task<string> GenerateLogisticsCoSN(IDbTransaction? transaction = null) => await GenerateSN(8, 5, transaction);
    public async Task<string> GenerateShippingSN(IDbTransaction? transaction = null) => await GenerateSN(12, 8, transaction);
    public async Task<string> GenerateReceivingSN(IDbTransaction? transaction = null) => await GenerateSN(13, 8, transaction);
    public async Task<string> GenerateExchangeSN(IDbTransaction? transaction = null) => await GenerateSN(2, 8, transaction);

    private async Task<string> GenerateSN(int flag, int length, IDbTransaction? transaction = null)
    {
        bool ownsTransaction = false;
        bool ownsConnection = false;
        IDbConnection db;
        IDbTransaction? txn = transaction;

        if (transaction != null)
        {
            db = transaction.Connection!;
        }
        else
        {
            db = await _dbFactory.CreateAsync();
            ownsConnection = true;
            // 无外部事务时自行创建事务，确保 UPDLOCK 锁定生效
            txn = db.BeginTransaction(IsolationLevel.ReadCommitted);
            ownsTransaction = true;
        }

        try
        {
            // 使用 UPDLOCK + HOLDLOCK 防止并发读取同一行产生重复单号
            var currentSn = await db.QueryFirstOrDefaultAsync<string>(
                "SELECT sn FROM serialnumber_new WITH (UPDLOCK, HOLDLOCK) WHERE id = @Id",
                new { Id = flag }, txn);

            string newSnValue;
            if (currentSn != null)
            {
                newSnValue = (long.Parse(currentSn) + 1).ToString();
                await db.ExecuteAsync(
                    "UPDATE serialnumber_new SET sn = @Sn WHERE id = @Id",
                    new { Sn = newSnValue, Id = flag }, txn);
            }
            else
            {
                newSnValue = "1";
                await db.ExecuteAsync(
                    "INSERT INTO serialnumber_new (id, memo, sn) VALUES (@Id, @Memo, @Sn)",
                    new { Id = flag, Memo = GetMemo(flag), Sn = newSnValue }, txn);
            }

            var paddedSn = long.Parse(newSnValue).ToString().PadLeft(length, '0');

            await db.ExecuteAsync(
                "UPDATE serialnumber SET sn = @Sn WHERE id = @Id",
                new { Sn = paddedSn, Id = flag }, txn);

            if (ownsTransaction) txn!.Commit();

            return paddedSn;
        }
        catch
        {
            if (ownsTransaction) txn!.Rollback();
            throw;
        }
        finally
        {
            if (ownsTransaction) txn?.Dispose();
            if (ownsConnection) db.Dispose();
        }
    }

    private static string GetMemo(int flag) => flag switch
    {
        1 => "进货",
        2 => "销售",
        3 => "供应商",
        4 => "员工",
        5 => "客户",
        6 => "连锁店号",
        7 => "计划单号",
        8 => "物流公司编号",
        12 => "物流发货编号",
        13 => "物流收货编号",
        _ => ""
    };
}
