using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Data.Infrastructure;

namespace QP11.Data.Repositories;

/// <summary>
/// 销售仓储，提供销售单据及明细的增删改查
/// </summary>
public class SellRepository : ISellRepository
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

    /// <summary>
    /// 根据单号获取销售单
    /// </summary>
    public async Task<BillSell?> GetBySnAsync(string sn)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<BillSell>(
            "SELECT * FROM bill_sell WHERE sn = @Sn", new { Sn = sn });
    }

    /// <summary>
    /// 按条件查询销售单列表
    /// </summary>
    public async Task<IEnumerable<BillSell>> GetListAsync(DateTime? startDate = null, DateTime? endDate = null, string? client = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"SELECT bill_sell.*, client_infor.name AS ClientName
                     FROM bill_sell
                     LEFT JOIN client_infor ON client_infor.cid = bill_sell.client
                     WHERE ISNULL(bill_sell.flag,0) != -1";
        if (startDate.HasValue) sql += " AND bill_sell.datetime >= @Start";
        if (endDate.HasValue) sql += " AND bill_sell.datetime < DATEADD(day, 1, @End)";
        if (!string.IsNullOrEmpty(client)) sql += " AND bill_sell.client = @Client";
        sql += " ORDER BY bill_sell.datetime DESC";
        return await db.QueryAsync<BillSell>(sql, new { Start = startDate, End = endDate, Client = client });
    }

    /// <summary>
    /// 根据单号获取销售明细
    /// </summary>
    public async Task<IEnumerable<DetailSell>> GetDetailsAsync(string sn)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<DetailSell>(
            "SELECT * FROM detail_sell WHERE sn = @Sn", new { Sn = sn });
    }

    /// <summary>
    /// 新增销售单
    /// </summary>
    public async Task<int> InsertBillAsync(BillSell bill, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO bill_sell (sn, client, worker, [operator], checkno, total, bill_total, discount_rate,
                    total_payment, bill_payment,
                    cash, collection, weixin, zhifubao, checks, yunfei, arrear, flag, datetime, memo)
                    VALUES (@Sn, @Client, @Worker, @Operator, @Checkno, @Total, @BillTotal, @DiscountRate,
                    @TotalPayment, @BillPayment,
                    @Cash, @Collection, @Weixin, @Zhifubao, @Checks, @Yunfei, @Arrear, @Flag, COALESCE(@Datetime, GETDATE()), @Memo)";
        var result = await db.ExecuteAsync(sql, bill, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 新增单条销售明细
    /// </summary>
    public async Task<int> InsertDetailAsync(DetailSell detail, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO detail_sell (sn, partid, partno, name, unit, place, amount, price, bill_price,
                    stotal, btotal, cb, cartype, car_mark, memo, tsn, datetime, flag, type)
                    VALUES (@Sn, @Partid, @Partno, @Name, @Unit, @Place, @Amount, @Price, @BillPrice,
                    @Stotal, @Btotal, @Cb, @Cartype, @CarMark, @Memo, @Tsn, COALESCE(@Datetime, GETDATE()), COALESCE(@Flag,1), COALESCE(@Type,0))";
        var result = await db.ExecuteAsync(sql, detail, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<int> InsertDetailsAsync(IEnumerable<DetailSell> details, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO detail_sell (sn, partid, partno, name, unit, place, amount, price, bill_price,
                    stotal, btotal, cb, cartype, car_mark, memo, tsn, datetime, flag, type)
                    VALUES (@Sn, @Partid, @Partno, @Name, @Unit, @Place, @Amount, @Price, @BillPrice,
                    @Stotal, @Btotal, @Cb, @Cartype, @CarMark, @Memo, @Tsn, COALESCE(@Datetime, GETDATE()), COALESCE(@Flag,1), COALESCE(@Type,0))";
        var result = await db.ExecuteAsync(sql, details, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<int> UpdateAsync(BillSell bill)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE bill_sell SET client=@Client, worker=@Worker, [operator]=@Operator,
                    checkno=@Checkno, total=@Total, bill_total=@BillTotal, discount_rate=@DiscountRate,
                    total_payment=@TotalPayment, bill_payment=@BillPayment,
                    cash=@Cash, collection=@Collection, weixin=@Weixin, zhifubao=@Zhifubao, yunfei=@Yunfei,
                    checks=@Checks, arrear=@Arrear, datetime=@Datetime, memo=@Memo
                    WHERE sn=@Sn";
        var result = await db.ExecuteAsync(sql, bill);
        // 同步更新明细表日期
        await db.ExecuteAsync("UPDATE detail_sell SET datetime=@Datetime WHERE sn=@Sn", bill);
        return result;
    }

    public async Task<int> UpdateAsync(BillSell bill, IDbTransaction? transaction)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"UPDATE bill_sell SET client=@Client, worker=@Worker, [operator]=@Operator,
                    checkno=@Checkno, total=@Total, bill_total=@BillTotal, discount_rate=@DiscountRate,
                    total_payment=@TotalPayment, bill_payment=@BillPayment,
                    cash=@Cash, collection=@Collection, weixin=@Weixin, zhifubao=@Zhifubao, yunfei=@Yunfei,
                    checks=@Checks, arrear=@Arrear, datetime=@Datetime, memo=@Memo
                    WHERE sn=@Sn";
        var result = await db.ExecuteAsync(sql, bill, transaction);
        // 同步更新明细表日期
        await db.ExecuteAsync("UPDATE detail_sell SET datetime=@Datetime WHERE sn=@Sn", bill, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 更新销售单状态
    /// </summary>
    public async Task<int> UpdateBillStatusAsync(string sn, int flag, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.ExecuteAsync("UPDATE bill_sell SET flag = @Flag WHERE sn = @Sn", new { Flag = flag, Sn = sn }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 更新销售单据备注
    /// </summary>
    public async Task<int> UpdateMemoAsync(string sn, string memo)
    {
        using var db = await CreateConnectionAsync();
        var sql = "UPDATE bill_sell SET memo = @Memo WHERE sn = @Sn";
        return await db.ExecuteAsync(sql, new { Sn = sn, Memo = memo });
    }

    /// <summary>
    /// 逻辑删除销售单
    /// </summary>
    public async Task<int> LogicDeleteBillAsync(string sn)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE bill_sell SET flag = -1 WHERE sn=@Sn", new { Sn = sn });
    }

    /// <summary>
    /// 删除销售单的所有明细（编辑时先删后插）
    /// </summary>
    public async Task<int> DeleteDetailsAsync(string sn)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM detail_sell WHERE sn=@Sn", new { Sn = sn });
    }

    public async Task<int> DeleteDetailsAsync(string sn, IDbTransaction? transaction)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.ExecuteAsync("DELETE FROM detail_sell WHERE sn=@Sn", new { Sn = sn }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<IEnumerable<dynamic>> GetDetailListAsync(DateTime? startDate = null, DateTime? endDate = null, string? client = null, string? worker = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"SELECT detail_sell.sn, detail_sell.partid, detail_sell.partno, detail_sell.name,
                    detail_sell.amount, detail_sell.price, detail_sell.bill_price,
                    detail_sell.cartype, detail_sell.car_mark, detail_sell.memo, bill_sell.datetime as datetime,
                    detail_sell.unit, detail_sell.stotal, detail_sell.btotal, detail_sell.id,
                    detail_sell.tsn, detail_sell.type, detail_sell.place, detail_sell.flag,
                    detail_sell.cb, detail_sell.part_th, detail_sell.part_gg, detail_sell.part_cclb,
                    ISNULL(bill_sell.flag, 0) as bill_flag,
                    client_infor.name as client, ISNULL(work_infor.name, bill_sell.worker) as worker
                    FROM detail_sell
                    LEFT JOIN bill_sell ON bill_sell.sn = detail_sell.sn
                    LEFT JOIN client_infor ON client_infor.cid = bill_sell.client
                    LEFT JOIN work_infor ON work_infor.workid = bill_sell.worker
                    WHERE detail_sell.amount <> 0 AND ISNULL(bill_sell.flag,0) != -1";
        if (startDate.HasValue) sql += " AND bill_sell.datetime >= @Start";
        if (endDate.HasValue) sql += " AND bill_sell.datetime < DATEADD(day, 1, @End)";
        if (!string.IsNullOrEmpty(client)) sql += " AND client_infor.name LIKE @Client";
        if (!string.IsNullOrEmpty(worker)) sql += " AND (work_infor.name LIKE @Worker OR bill_sell.worker LIKE @Worker)";
        sql += " ORDER BY detail_sell.sn DESC";
        return await db.QueryAsync<dynamic>(sql, new { Start = startDate, End = endDate, Client = $"%{client}%", Worker = $"%{worker}%" });
    }

    /// <summary>
    /// 获取今日配件销售排行（按销量降序，前 N 条，含实时库存）
    /// SQL Server 2000 不支持 ROW_NUMBER() 和参数化 TOP，用字符串拼接 TOP 值
    /// </summary>
    public async Task<IEnumerable<dynamic>> GetTodayPartsRankingAsync(DateTime today, int top = 10)
    {
        using var db = await CreateConnectionAsync();
        var tomorrow = today.AddDays(1);
        var sql = $@"SELECT TOP {top}
                    d.partid AS PartId, d.partno AS PartNo, d.name AS PartName,
                    MAX(d.cartype) AS Cartype,
                    SUM(d.amount) AS SaleAmount, SUM(d.stotal) AS SaleTotal,
                    ISNULL(ps.amount, 0) AS StockAmount
                    FROM detail_sell d
                    INNER JOIN bill_sell b ON b.sn = d.sn
                    LEFT JOIN part_stock ps ON ps.partid = d.partid
                    WHERE b.datetime >= @Start AND b.datetime < DATEADD(day, 1, @End)
                      AND ISNULL(b.flag, 0) != -1
                      AND d.amount > 0
                    GROUP BY d.partid, d.partno, d.name, ps.amount
                    ORDER BY SUM(d.amount) DESC";
        return await db.QueryAsync<dynamic>(sql, new { Start = today, End = today });
    }

    /// <summary>
    /// 根据单号列表查询挂账信息（仅返回有挂账的）
    /// </summary>
    public async Task<IEnumerable<ArrearBillInfo>> GetArrearBillsAsync(IEnumerable<string> sns)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"SELECT sn, ISNULL(arrear, 0) AS Arrear, client AS ClientId
                    FROM bill_sell
                    WHERE sn IN @Sns AND ISNULL(arrear, 0) > 0 AND ISNULL(flag, 0) <> -1";
        return await db.QueryAsync<ArrearBillInfo>(sql, new { Sns = sns });
    }

    /// <summary>
    /// 根据单号列表查询挂账信息（返回所有，包括arrear=0的）
    /// </summary>
    public async Task<IEnumerable<ArrearBillInfo>> GetArrearBillsAllAsync(IEnumerable<string> sns)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"SELECT sn, ISNULL(arrear, 0) AS Arrear, client AS ClientId
                    FROM bill_sell
                    WHERE sn IN @Sns AND ISNULL(flag, 0) <> -1";
        return await db.QueryAsync<ArrearBillInfo>(sql, new { Sns = sns });
    }

    /// <summary>
    /// 一键做账：将指定单据的挂账金额转为已收款，同步更新付款方式字段
    /// </summary>
    public async Task<int> BatchSettleArrearAsync(IEnumerable<string> sns, string payMethod)
    {
        var snList = sns.ToList();
        if (snList.Count == 0) return 0;

        // 根据付款方式确定更新哪个字段
        var payColumn = payMethod switch
        {
            "weixin" => "weixin",
            "zhifubao" => "zhifubao",
            "checks" => "checks",
            _ => "cash"
        };

        using var db = await CreateConnectionAsync();
        using var txn = db.BeginTransaction();

        try
        {
            // 1. 更新 arrearage: charge += arrear（退货单取绝对值）
            var updateArrearSql = @"UPDATE arrearage SET
                charge = ISNULL(arrearage.charge, 0) +
                    CASE WHEN EXISTS(
                        SELECT 1 FROM bill_sell b WHERE b.sn = arrearage.sn AND (b.flag=2 OR b.total<0)
                    )
                    THEN (SELECT ABS(ISNULL(arrear,0)) FROM bill_sell WHERE bill_sell.sn = arrearage.sn)
                    ELSE (SELECT ISNULL(arrear,0) FROM bill_sell WHERE bill_sell.sn = arrearage.sn)
                    END
                WHERE sn IN @Sns";
            await db.ExecuteAsync(updateArrearSql, new { Sns = snList }, txn);

            // 2. 更新 bill_sell: arrear 清零 + 付款方式字段增加
            var updateSql = $@"UPDATE bill_sell SET
                arrear = 0,
                {payColumn} = ISNULL({payColumn}, 0) + ISNULL(
                    CASE WHEN (flag=2 OR total<0) THEN ABS(arrear) ELSE arrear END, 0)
                WHERE sn IN @Sns AND ABS(ISNULL(arrear, 0)) > 0.01 AND ISNULL(flag, 0) <> -1";
            var rows = await db.ExecuteAsync(updateSql, new { Sns = snList }, txn);

            txn.Commit();
            return rows;
        }
        catch
        {
            txn.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 分页查询销售单列表
    /// SQL Server 2000 不支持 ROW_NUMBER()，嵌套 TOP 在深分页时 O(n²) 超时，
    /// 因此采用轻量列全量查询 + 内存分页，仅选7列减少传输开销
    /// </summary>
    public async Task<(IEnumerable<dynamic> Data, int Total)> GetPagedOrdersAsync(DateTime? start, DateTime? end, string? client, int page, int pageSize)
    {
        using var db = await CreateConnectionAsync();
        var where = "WHERE ISNULL(b.flag,0) != -1";
        if (start.HasValue) where += " AND b.datetime >= @Start";
        if (end.HasValue) where += " AND b.datetime < DATEADD(day, 1, @End)";
        if (!string.IsNullOrEmpty(client)) where += " AND b.client = @Client";

        var countSql = $"SELECT COUNT(*) FROM bill_sell b {where}";
        var total = await db.ExecuteScalarAsync<int>(countSql, new { Start = start, End = end, Client = client });

        if (total == 0) return (Array.Empty<dynamic>(), 0);

        // 仅查询轻量列（7列），避免 SELECT * 传输冗余字段
        var sql = $@"
            SELECT b.sn AS Sn, ISNULL(c.name, b.client) AS ClientName,
                   ISNULL(w.name, b.worker) AS WorkerName,
                   b.total AS Total, b.bill_total AS BillTotal,
                   b.flag AS Flag, b.datetime AS Datetime
            FROM bill_sell b
            LEFT JOIN client_infor c ON c.cid = b.client
            LEFT JOIN work_infor w ON w.workid = b.worker
            {where}
            ORDER BY b.sn DESC";

        var allData = (await db.QueryAsync(sql, new { Start = start, End = end, Client = client })).ToList();

        // 内存分页：SQL 2000 无高效服务端分页方案，此方式在日期筛选后数据量可控
        var pagedData = allData.Skip((page - 1) * pageSize).Take(pageSize);
        return (pagedData, total);
    }

    // IRepository<BillSell> 显式实现
    Task<BillSell?> IRepository<BillSell>.GetByIdAsync(object id) => throw new NotImplementedException();
    Task<IEnumerable<BillSell>> IRepository<BillSell>.GetAllAsync() => GetListAsync();
    Task<int> IRepository<BillSell>.InsertAsync(BillSell entity, IDbTransaction? transaction) => InsertBillAsync(entity, transaction);
    Task<int> IRepository<BillSell>.UpdateAsync(BillSell entity, IDbTransaction? transaction) => throw new NotImplementedException("请使用 UpdateAsync(BillSell bill)");
    Task<int> IRepository<BillSell>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<BillSell>.CountAsync() => throw new NotImplementedException();
}
