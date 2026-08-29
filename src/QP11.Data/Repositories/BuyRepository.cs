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

/// <summary>
/// 采购仓储，提供采购单据及明细的增删改查
/// </summary>
public class BuyRepository : IBuyRepository
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
    /// 根据单号获取采购单
    /// </summary>
    public async Task<BillBuy?> GetBySnAsync(string sn, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.QueryFirstOrDefaultAsync<BillBuy>(
            "SELECT * FROM bill_buy WHERE sn = @Sn", new { Sn = sn }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 按日期范围查询采购单列表
    /// </summary>
    public async Task<IEnumerable<BillBuy>> GetListAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"SELECT * FROM bill_buy WHERE 1=1";
        if (startDate.HasValue) sql += " AND datetime >= @Start";
        if (endDate.HasValue) sql += " AND datetime < DATEADD(day, 1, @End)";
        sql += " ORDER BY datetime DESC";
        return await db.QueryAsync<BillBuy>(sql, new { Start = startDate, End = endDate });
    }

    public async Task<IEnumerable<BillBuy>> GetListByFlagAsync(int flag)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"SELECT * FROM bill_buy WHERE flag = @Flag ORDER BY datetime DESC";
        return await db.QueryAsync<BillBuy>(sql, new { Flag = flag });
    }

    /// <summary>
    /// 根据单号获取采购明细
    /// </summary>
    public async Task<IEnumerable<DetailBuy>> GetDetailsAsync(string sn)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<DetailBuy>(
            "SELECT * FROM detail_buy WHERE sn = @Sn", new { Sn = sn });
    }

    /// <summary>
    /// 新增采购单
    /// </summary>
    public async Task<int> InsertBillAsync(BillBuy bill, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO bill_buy (sn, supplier, worker, [operator], invoice, total, cash, checks, 
                    arrear, zhifubao, weixin, yunfei, flag, datetime, memo)
                    VALUES (@Sn, @Supplier, @Worker, @Operator, @Invoice, @Total, @Cash, @Checks,
                    @Arrear, @Zhifubao, @Weixin, @Yunfei, @Flag, COALESCE(@Datetime, GETDATE()), @Memo)";
        var result = await db.ExecuteAsync(sql, bill, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 批量新增采购明细
    /// </summary>
    public async Task<int> InsertDetailsAsync(IEnumerable<DetailBuy> details, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO detail_buy (sn, partid, partno, name, amount, unit, carname, cartype,
                    inprice, intotal, pfprice, lsprice, place, class, memo, tsn, datetime, type,
                    part_gg, part_th, part_cclb, part_bzq, part_bzrq)
                    VALUES (@Sn, @Partid, @Partno, @Name, @Amount, @Unit, @Carname, @Cartype,
                    @Inprice, @Stotal, @Pfprice, @Lsprice, @Place, @Class, @Memo, @Tsn, COALESCE(@Datetime, GETDATE()),
                    COALESCE(@Type,0),
                    @PartGg, @PartTh, @PartCclb, @PartBzq, @PartBzrq)";
        var result = await db.ExecuteAsync(sql, details, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<int> InsertDetailAsync(DetailBuy detail, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO detail_buy (sn, partid, partno, name, amount, unit, carname, cartype,
                    inprice, intotal, pfprice, lsprice, place, class, memo, tsn, datetime, type,
                    part_gg, part_th, part_cclb, part_bzq, part_bzrq)
                    VALUES (@Sn, @Partid, @Partno, @Name, @Amount, @Unit, @Carname, @Cartype,
                    @Inprice, @Stotal, @Pfprice, @Lsprice, @Place, @Class, @Memo, @Tsn, COALESCE(@Datetime, GETDATE()),
                    COALESCE(@Type,0),
                    @PartGg, @PartTh, @PartCclb, @PartBzq, @PartBzrq)";
        var result = await db.ExecuteAsync(sql, detail, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<int> UpdateAsync(BillBuy bill)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE bill_buy SET supplier=@Supplier, worker=@Worker, [operator]=@Operator,
                    invoice=@Invoice, total=@Total, cash=@Cash, checks=@Checks, arrear=@Arrear,
                    zhifubao=@Zhifubao, weixin=@Weixin, yunfei=@Yunfei, datetime=@Datetime, memo=@Memo
                    WHERE sn=@Sn";
        var result = await db.ExecuteAsync(sql, bill);
        // 同步更新明细表日期
        await db.ExecuteAsync("UPDATE detail_buy SET datetime=@Datetime WHERE sn=@Sn", bill);
        return result;
    }

    public async Task<int> UpdateAsync(BillBuy bill, IDbTransaction? transaction)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"UPDATE bill_buy SET supplier=@Supplier, worker=@Worker, [operator]=@Operator,
                    invoice=@Invoice, total=@Total, cash=@Cash, checks=@Checks, arrear=@Arrear,
                    zhifubao=@Zhifubao, weixin=@Weixin, yunfei=@Yunfei, datetime=@Datetime, memo=@Memo
                    WHERE sn=@Sn";
        var result = await db.ExecuteAsync(sql, bill, transaction);
        // 同步更新明细表日期
        await db.ExecuteAsync("UPDATE detail_buy SET datetime=@Datetime WHERE sn=@Sn", bill, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 物理删除采购单头（作废单据时与明细、欠款在同一事务内删除）
    /// </summary>
    public async Task<int> DeleteBillAsync(string sn, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.ExecuteAsync("DELETE FROM bill_buy WHERE sn = @Sn", new { Sn = sn }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<int> DeleteDetailsBySnAsync(string sn, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.ExecuteAsync("DELETE FROM detail_buy WHERE sn = @Sn", new { Sn = sn }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<string?> GetWorkerNameAsync(string workid)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<string>(
            "SELECT name FROM work_infor WHERE workid=@Workid", new { Workid = workid });
    }

    public async Task<string> ResolveWorkerIdAsync(string workerName)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<string>(
            "SELECT workid FROM work_infor WHERE name=@Name", new { Name = workerName }) ?? workerName;
    }

    /// <summary>
    /// 更新采购单状态
    /// </summary>
    public async Task<int> UpdateBillStatusAsync(string sn, int flag, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.ExecuteAsync("UPDATE bill_buy SET flag = @Flag WHERE sn = @Sn", new { Flag = flag, Sn = sn }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 根据配件ID查询所有进货记录（含供应商信息），用于销售退货选择关联进货单
    /// </summary>
    public async Task<IEnumerable<dynamic>> GetBuyDetailsByPartIdAsync(long partid)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"
            SELECT d.sn, d.amount, d.inprice, d.intotal, d.datetime,
                   b.supplier, s.name AS supplier_name, s.sid AS supplier_sid
            FROM detail_buy d
            INNER JOIN bill_buy b ON b.sn = d.sn
            LEFT JOIN supplier_infor s ON s.sid = b.supplier
            WHERE d.partid = @Partid
              AND d.amount > 0
              AND ISNULL(b.flag, 0) <> 3
            ORDER BY d.datetime DESC";
        return await db.QueryAsync<dynamic>(sql, new { Partid = partid });
    }

    public async Task<IEnumerable<dynamic>> GetDetailListAsync(DateTime? startDate = null, DateTime? endDate = null, string? supplier = null, string? worker = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"SELECT detail_buy.partno, detail_buy.name, detail_buy.amount, detail_buy.unit,
                    detail_buy.carname, detail_buy.cartype, detail_buy.inprice, detail_buy.intotal,
                    detail_buy.pfprice, detail_buy.lsprice, detail_buy.place, detail_buy.memo,
                    detail_buy.partid, detail_buy.sn, detail_buy.id, detail_buy.datetime,
                    detail_buy.class, detail_buy.type,
                    bill_buy.flag, supplier_infor.name as supplier, work_infor.name as worker
                    FROM detail_buy
                    LEFT JOIN bill_buy ON detail_buy.sn = bill_buy.sn
                    LEFT JOIN supplier_infor ON supplier_infor.sid = bill_buy.supplier
                    LEFT JOIN work_infor ON work_infor.workid = bill_buy.worker
                    WHERE 1=1";
        if (startDate.HasValue) sql += " AND detail_buy.datetime >= @Start";
        if (endDate.HasValue) sql += " AND detail_buy.datetime < DATEADD(day, 1, @End)";
        if (!string.IsNullOrEmpty(supplier)) sql += " AND supplier_infor.name LIKE @Supplier";
        if (!string.IsNullOrEmpty(worker)) sql += " AND work_infor.name LIKE @Worker";
        sql += " ORDER BY detail_buy.sn ASC";
        return await db.QueryAsync<dynamic>(sql, new { Start = startDate, End = endDate, Supplier = $"%{supplier}%", Worker = $"%{worker}%" });
    }

    /// <summary>
    /// 按条件查询采购单据列表（含供应商/采购员名称）
    /// </summary>
    public async Task<IEnumerable<dynamic>> GetBillListAsync(DateTime? startDate = null, DateTime? endDate = null, string? supplier = null, string? worker = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"SELECT b.sn, b.datetime, b.total, b.cash, b.checks, b.arrear,
                    b.zhifubao, b.weixin, b.yunfei, b.flag, b.memo, b.type,
                    s.name AS supplier, w.name AS worker
                    FROM bill_buy b
                    LEFT JOIN supplier_infor s ON s.sid = b.supplier
                    LEFT JOIN work_infor w ON w.workid = b.worker
                    WHERE 1=1";
        if (startDate.HasValue) sql += " AND b.datetime >= @Start";
        if (endDate.HasValue) sql += " AND b.datetime < DATEADD(day, 1, @End)";
        if (!string.IsNullOrEmpty(supplier)) sql += " AND s.name LIKE @Supplier";
        if (!string.IsNullOrEmpty(worker)) sql += " AND w.name LIKE @Worker";
        sql += " ORDER BY b.datetime DESC";
        return await db.QueryAsync<dynamic>(sql, new { Start = startDate, End = endDate, Supplier = $"%{supplier}%", Worker = $"%{worker}%" });
    }

    // IRepository<BillBuy> 显式实现
    Task<BillBuy?> IRepository<BillBuy>.GetByIdAsync(object id) => throw new NotImplementedException();
    Task<IEnumerable<BillBuy>> IRepository<BillBuy>.GetAllAsync() => GetListAsync();
    Task<int> IRepository<BillBuy>.InsertAsync(BillBuy entity, IDbTransaction? transaction) => InsertBillAsync(entity, transaction);
    Task<int> IRepository<BillBuy>.UpdateAsync(BillBuy entity, IDbTransaction? transaction) => throw new NotImplementedException("请使用 UpdateAsync(BillBuy bill)");
    Task<int> IRepository<BillBuy>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<BillBuy>.CountAsync() => throw new NotImplementedException();
}
