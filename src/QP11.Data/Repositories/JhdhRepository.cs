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
/// 计划订货仓储，提供计划单据及明细的增删改查
/// </summary>
public class JhdhRepository : IJhdhRepository
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
    /// 根据单号获取计划单
    /// </summary>
    public async Task<BillJhdh?> GetBySnAsync(string sn, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.QueryFirstOrDefaultAsync<BillJhdh>(
            "SELECT * FROM bill_jhdh WHERE sn = @Sn", new { Sn = sn }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 按日期范围查询计划单列表
    /// </summary>
    public async Task<IEnumerable<BillJhdh>> GetListAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = "SELECT * FROM bill_jhdh WHERE 1=1";
        if (startDate.HasValue) sql += " AND datetime >= @Start";
        if (endDate.HasValue) sql += " AND datetime < DATEADD(day, 1, @End)";
        sql += " ORDER BY datetime DESC";
        return await db.QueryAsync<BillJhdh>(sql, new { Start = startDate, End = endDate });
    }

    /// <summary>
    /// 按状态查询计划单列表
    /// </summary>
    public async Task<IEnumerable<BillJhdh>> GetListByFlagAsync(int flag)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<BillJhdh>(
            "SELECT * FROM bill_jhdh WHERE flag = @Flag ORDER BY datetime DESC", new { Flag = flag });
    }

    /// <summary>
    /// 根据单号获取计划单明细
    /// </summary>
    public async Task<IEnumerable<DetailJhdh>> GetDetailsAsync(string sn)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<DetailJhdh>(
            "SELECT * FROM detail_jhdh WHERE sn = @Sn", new { Sn = sn });
    }

    /// <summary>
    /// 新增计划单（bill_jhdh 实际列：sn, supplier, worker, operator, total, datetime, memo, flag）
    /// </summary>
    public async Task<int> InsertBillAsync(BillJhdh bill, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO bill_jhdh (sn, supplier, worker, [operator], total, flag, datetime, memo)
                    VALUES (@Sn, @Supplier, @Worker, @Operator, @Total, @Flag, COALESCE(@Datetime, GETDATE()), @Memo)";
        var result = await db.ExecuteAsync(sql, bill, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 批量新增计划单明细
    /// detail_jhdh 实际列：sn, partid, partno, name, carname, cartype, unit, amount, price, total,
    ///                      wayed, waying, lsprice, pfprice, flag, memo, datetime
    /// </summary>
    public async Task<int> InsertDetailsAsync(IEnumerable<DetailJhdh> details, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO detail_jhdh (sn, partid, partno, name, carname, cartype, unit,
                    amount, price, total, wayed, waying, lsprice, pfprice, flag, datetime, memo)
                    VALUES (@Sn, @Partid, @Partno, @Name, @Carname, @Cartype, @Unit,
                    @Amount, @Price, @Total, 0, 0, @Lsprice, @Pfprice, 0, COALESCE(@Datetime, GETDATE()), @Memo)";
        var result = await db.ExecuteAsync(sql, details, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<int> InsertDetailAsync(DetailJhdh detail, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO detail_jhdh (sn, partid, partno, name, carname, cartype, unit,
                    amount, price, total, wayed, waying, lsprice, pfprice, flag, datetime, memo)
                    VALUES (@Sn, @Partid, @Partno, @Name, @Carname, @Cartype, @Unit,
                    @Amount, @Price, @Total, 0, 0, @Lsprice, @Pfprice, 0, COALESCE(@Datetime, GETDATE()), @Memo)";
        var result = await db.ExecuteAsync(sql, detail, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<int> UpdateAsync(BillJhdh bill)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE bill_jhdh SET supplier=@Supplier, worker=@Worker, [operator]=@Operator,
                    total=@Total, datetime=@Datetime, memo=@Memo
                    WHERE sn=@Sn";
        var result = await db.ExecuteAsync(sql, bill);
        // 同步更新明细表日期
        await db.ExecuteAsync("UPDATE detail_jhdh SET datetime=@Datetime WHERE sn=@Sn", bill);
        return result;
    }

    public async Task<int> UpdateAsync(BillJhdh bill, IDbTransaction? transaction)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"UPDATE bill_jhdh SET supplier=@Supplier, worker=@Worker, [operator]=@Operator,
                    total=@Total, datetime=@Datetime, memo=@Memo
                    WHERE sn=@Sn";
        var result = await db.ExecuteAsync(sql, bill, transaction);
        // 同步更新明细表日期
        await db.ExecuteAsync("UPDATE detail_jhdh SET datetime=@Datetime WHERE sn=@Sn", bill, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 更新计划单状态
    /// </summary>
    public async Task<int> UpdateBillStatusAsync(string sn, int flag, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.ExecuteAsync(
            "UPDATE bill_jhdh SET flag = @Flag WHERE sn = @Sn", new { Flag = flag, Sn = sn }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 删除指定单号的所有明细
    /// </summary>
    public async Task<int> DeleteDetailsBySnAsync(string sn, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.ExecuteAsync(
            "DELETE FROM detail_jhdh WHERE sn = @Sn", new { Sn = sn }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    // IRepository<BillJhdh> 显式实现
    Task<BillJhdh?> IRepository<BillJhdh>.GetByIdAsync(object id) => throw new NotImplementedException();
    Task<IEnumerable<BillJhdh>> IRepository<BillJhdh>.GetAllAsync() => GetListAsync();
    Task<int> IRepository<BillJhdh>.InsertAsync(BillJhdh entity, IDbTransaction? transaction) => InsertBillAsync(entity, transaction);
    Task<int> IRepository<BillJhdh>.UpdateAsync(BillJhdh entity, IDbTransaction? transaction) => throw new NotImplementedException("请使用 UpdateAsync(BillJhdh bill)");
    Task<int> IRepository<BillJhdh>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<BillJhdh>.CountAsync() => throw new NotImplementedException();
}
