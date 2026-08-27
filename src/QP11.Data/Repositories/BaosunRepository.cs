using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Data.Infrastructure;

namespace QP11.Data.Repositories;

public class BaosunRepository : IBaosunRepository
{
    protected DbConnection CreateConnection() => DatabaseFactory.Create();

    /// <summary>创建并异步打开连接，避免 UI 线程同步阻塞</summary>
    protected async Task<DbConnection> CreateConnectionAsync()
    {
        var db = DatabaseFactory.Create();
        if (db.State != ConnectionState.Open)
            await db.OpenAsync();
        return db;
    }

    public async Task<IEnumerable<BillBaosun>> GetListAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        using var db = await CreateConnectionAsync();
        // 报损单使用 bill_sell 表，flag=3 标识报损记录
        // LEFT JOIN work_infor 将经手人工号转为姓名，在查询模式下正确显示
        var sql = @"SELECT b.sn, ISNULL(wi.name, b.worker) AS worker, b.[operator], b.total, b.flag, b.type, b.datetime, b.memo
                    FROM bill_sell b
                    LEFT JOIN work_infor wi ON wi.workid = b.worker
                    WHERE b.flag=3";
        if (startDate.HasValue) sql += " AND b.datetime >= @Start";
        if (endDate.HasValue) sql += " AND b.datetime < DATEADD(day, 1, @End)";
        sql += " ORDER BY b.datetime DESC";
        return await db.QueryAsync<BillBaosun>(sql, new { Start = startDate, End = endDate });
    }

    public async Task<BillBaosun?> GetBySnAsync(string sn)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<BillBaosun>(
            "SELECT * FROM bill_sell WHERE sn = @Sn AND flag=3", new { Sn = sn });
    }

    public async Task<IEnumerable<DetailBaosun>> GetDetailsAsync(string sn)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<DetailBaosun>(
            "SELECT * FROM detail_sell WHERE sn = @Sn", new { Sn = sn });
    }

    public async Task<int> InsertBillAsync(BillBaosun bill)
    {
        using var db = await CreateConnectionAsync();
        // 报损单写入 bill_sell 表，flag=3 报损，type=0（与旧数据格式一致）
        // client 写入"配件报损"特殊客户 cid（见 BusinessConstants.BaosunClientId），
        // 与历史惯例一致：旧 PB 系统 client 存 02288/03136（client_infor 中 name='配件报损'）
        // worker 存姓名，operator 存当前登录用户名
        var sql = @"INSERT INTO bill_sell (sn, client, worker, [operator], total, bill_total, discount_rate,
                    total_payment, bill_payment, cash, collection, checks, arrear, flag, type, datetime, checkno, memo)
                    VALUES (@Sn, @Client, @Worker, @Operator, @Total, 0, 0, @Total, 0, @Total, 0, 0, 0, 3, 0, GETDATE(), '', @Memo)";
        return await db.ExecuteAsync(sql, bill);
    }

    public async Task<int> InsertDetailAsync(DetailBaosun detail)
    {
        using var db = await CreateConnectionAsync();
        // 报损明细写入 detail_sell 表，flag=3 报损，type=0（与旧数据格式一致）
        // 进价存 price 列，小计存 stotal 列，成本存 cb 列
        var sql = @"INSERT INTO detail_sell (sn, partid, partno, name, amount, unit, price, bill_price,
                    stotal, btotal, cartype, car_mark, memo, flag, type, datetime, cb)
                    VALUES (@Sn, @Partid, @Partno, @Name, @Amount, @Unit, @Inprice, 0,
                    @Intotal, 0, @Cartype, '', @Memo, 3, 0, GETDATE(), @Cb)";
        return await db.ExecuteAsync(sql, detail);
    }

    public async Task<int> UpdateBillStatusAsync(string sn, int flag)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE bill_sell SET flag = @Flag WHERE sn = @Sn AND flag=3", new { Flag = flag, Sn = sn });
    }
}
