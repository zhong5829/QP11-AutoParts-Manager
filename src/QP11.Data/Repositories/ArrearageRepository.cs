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
/// 欠款仓储，提供应收应付记录的查询和新增功能
/// </summary>
public class ArrearageRepository : IArrearageRepository
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
    /// 根据客户/供应商编号获取欠款记录
    /// </summary>
    public async Task<IEnumerable<Arrearage>> GetByClientAsync(string cid)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<Arrearage>(
            "SELECT id, bid, sn, total, charge, [operator], type, btype, datetime FROM arrearage WHERE bid = @Bid ORDER BY datetime DESC", new { Bid = cid });
    }

    /// <summary>
    /// 按类型和日期范围查询欠款列表
    /// </summary>
    public async Task<IEnumerable<Arrearage>> GetListAsync(int? type = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = "SELECT id, bid, sn, total, charge, [operator], type, btype, datetime FROM arrearage WHERE 1=1";
        if (type.HasValue) sql += " AND type = @Type";
        if (startDate.HasValue) sql += " AND datetime >= @Start";
        if (endDate.HasValue) sql += " AND datetime < DATEADD(day, 1, @End)";
        sql += " ORDER BY datetime DESC";
        return await db.QueryAsync<Arrearage>(sql, new { Type = type, Start = startDate, End = endDate });
    }

    /// <summary>
    /// 获取指定客户/供应商的欠款合计
    /// 正常单：total - charge；退货单：-(total - charge)
    /// 负数单（total<0）：直接按 total - charge（已带负号，不再取反）
    /// </summary>
    public async Task<decimal> GetClientArrearTotalAsync(string cid)
    {
        using var db = await CreateConnectionAsync();
        var total = await db.QueryFirstOrDefaultAsync<decimal?>(
            @"SELECT ISNULL(SUM(
                CASE WHEN arrearage.total < 0
                        THEN arrearage.total - ISNULL(arrearage.charge,0)
                     WHEN bs.flag=2 OR bs.total<0 -- BillFlag.Returned
                        THEN -(arrearage.total - ISNULL(arrearage.charge,0))
                     ELSE arrearage.total - ISNULL(arrearage.charge,0)
                END
              ), 0) FROM arrearage
              LEFT JOIN bill_sell bs ON arrearage.sn = bs.sn AND arrearage.type = 2
              WHERE bid = @Cid
              AND CASE WHEN arrearage.total < 0
                        THEN arrearage.total - ISNULL(arrearage.charge,0)
                       WHEN bs.flag=2 OR bs.total<0 -- BillFlag.Returned
                        THEN -(arrearage.total - ISNULL(arrearage.charge,0))
                       ELSE (arrearage.total - ISNULL(arrearage.charge,0))
                  END <> 0",
            new { Bid = cid });
        return total ?? 0m;
    }

    /// <summary>
    /// 新增欠款记录
    /// </summary>
    public async Task<int> InsertAsync(Arrearage entity, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO arrearage (bid, type, btype, total, sn, datetime)
                    VALUES (@Bid, @Type, @Btype, @Total, @Sn, GETDATE())";
        var result = await db.ExecuteAsync(sql, entity, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 获取客户/供应商列表（含欠款合计）
    /// 正常单：total - charge；退货单：-(total - charge)
    /// 负数单（total<0）：直接按 total - charge（已带负号，不再取反）
    /// </summary>
    public async Task<IEnumerable<dynamic>> GetClientArrearageListAsync(int type, string? keyword = null)
    {
        using var db = await CreateConnectionAsync();
        var tableName = type == 1 ? "supplier_infor" : "client_infor";
        var idCol = type == 1 ? "sid" : "cid";
        var joinCol = type == 1 ? "supplier_infor.sid" : "client_infor.cid";
        var sql = $@"SELECT {joinCol} as bid, {tableName}.name as name,
                    ISNULL(SUM(CASE WHEN arrearage.total < 0
                            THEN arrearage.total - ISNULL(arrearage.charge,0)
                        WHEN bs.flag=2 OR bs.total<0 -- BillFlag.Returned
                            THEN -(arrearage.total - ISNULL(arrearage.charge,0))
                        ELSE arrearage.total - ISNULL(arrearage.charge,0)
                    END), 0) as total_je
                    FROM {tableName}
                    INNER JOIN arrearage ON arrearage.bid = {joinCol} AND arrearage.type = @Type
                    LEFT JOIN bill_sell bs ON arrearage.sn = bs.sn AND arrearage.type = 2
                    WHERE 1=1";
        if (!string.IsNullOrEmpty(keyword))
            sql += $" AND ({tableName}.name LIKE @Kw OR {tableName}.{idCol} LIKE @Kw OR {tableName}.name_py LIKE @Kw)";
        sql += $@" GROUP BY {joinCol}, {tableName}.name
                  HAVING SUM(CASE WHEN arrearage.total < 0
                            THEN arrearage.total - ISNULL(arrearage.charge,0)
                        WHEN bs.flag=2 OR bs.total<0 -- BillFlag.Returned
                            THEN -(arrearage.total - ISNULL(arrearage.charge,0))
                        ELSE arrearage.total - ISNULL(arrearage.charge,0)
                    END) <> 0
                  ORDER BY {tableName}.name";
        return await db.QueryAsync<dynamic>(sql, new { Type = type, Kw = $"%{keyword}%" });
    }

    /// <summary>
    /// 获取欠款明细（只返回未结清记录）
    /// 正常单：je=total, charge=charge, owe=total-charge
    /// 退货单：je=-total(红), charge=-charge(红), owe=-(total-charge)
    /// 负数单（total<0）：je=total(红), charge=charge, owe=total-charge（已带负号，不再取反）
    /// </summary>
    public async Task<IEnumerable<dynamic>> GetArrearageDetailByBidAsync(string bid, int? type = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"SELECT a.id, a.bid, a.sn,
                    CASE WHEN a.total < 0 THEN a.total
                         WHEN bs.flag=2 OR bs.total<0 THEN -a.total ELSE a.total END as je, -- BillFlag.Returned
                    CASE WHEN a.total < 0 THEN ISNULL(a.charge,0)
                         WHEN bs.flag=2 OR bs.total<0 THEN -ISNULL(a.charge,0)
                         ELSE ISNULL(a.charge,0) END as charge, -- BillFlag.Returned
                    a.[operator], a.type, a.btype, a.datetime,
                    CASE WHEN bs.flag=2 OR bs.total<0 THEN 1 ELSE 0 END as is_return, -- BillFlag.Returned
                    CASE WHEN a.total < 0 THEN (ISNULL(a.total,0) - ISNULL(a.charge,0))
                         WHEN bs.flag=2 OR bs.total<0 THEN -(ISNULL(a.total,0) - ISNULL(a.charge,0))
                         ELSE (ISNULL(a.total,0) - ISNULL(a.charge,0))
                    END as owe
                    FROM arrearage a
                    LEFT JOIN bill_sell bs ON a.sn = bs.sn AND a.type = 2
                    LEFT JOIN bill_buy bb ON a.sn = bb.sn AND a.type = 1
                    WHERE a.bid = @Bid
                    AND CASE WHEN a.total < 0 THEN (ISNULL(a.total,0) - ISNULL(a.charge,0))
                             WHEN bs.flag=2 OR bs.total<0 THEN -(ISNULL(a.total,0) - ISNULL(a.charge,0))
                             ELSE (ISNULL(a.total,0) - ISNULL(a.charge,0))
                        END <> 0";
        if (type.HasValue) sql += " AND a.type = @Type";
        sql += " ORDER BY a.datetime DESC";
        return await db.QueryAsync<dynamic>(sql, new { Bid = bid, Type = type });
    }

    public async Task<int> UpdateChargeAsync(long id, decimal delta, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.ExecuteAsync("UPDATE arrearage SET charge = ISNULL(charge,0) + @Delta WHERE id = @Id",
            new { Delta = delta, Id = id }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 更新收款金额到charge字段，同时同步更新关联单据(bill_sell/bill_buy)的挂账
    /// </summary>
    public async Task<int> UpdatePaymentAsync(long id, decimal amount, string payMethod, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();

        // 1. 更新 arrearage 的 charge 字段
        var result = await db.ExecuteAsync(
            "UPDATE arrearage SET charge = ISNULL(charge,0) + @Amount WHERE id = @Id",
            new { Amount = amount, Id = id }, transaction);

        // 2. 同步更新关联单据的挂账字段 + 付款方式字段
        var row = await db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT sn, type, btype FROM arrearage WHERE id = @Id", new { Id = id }, transaction);
        if (row == null) { if (transaction == null) db.Dispose(); return result; }

        int typeVal = row.type;
        string sn = row.sn;

        // 根据付款方式确定更新哪个字段
        string payColumn = payMethod switch
        {
            "微信" => "weixin",
            "支付宝" => "zhifubao",
            _ => "cash"  // 现金或其他
        };

        if (typeVal == 2)
        {
            // 销售应收 → 更新 bill_sell
            var isReturn = await db.QueryFirstOrDefaultAsync<int?>(
                "SELECT CASE WHEN flag=2 OR total<0 THEN 1 ELSE 0 END FROM bill_sell WHERE sn = @Sn",
                new { Sn = sn }, transaction) == 1;
            if (isReturn)
            {
                // 退货单：挂账清零，付款字段增加（退货收到退款）
                await db.ExecuteAsync(
                    $"UPDATE bill_sell SET arrear = 0, {payColumn} = ISNULL({payColumn},0) + @Amount WHERE sn = @Sn",
                    new { Amount = amount, Sn = sn }, transaction);
            }
            else
            {
                // 正常销售单：挂账减少，付款字段增加
                await db.ExecuteAsync(
                    $"UPDATE bill_sell SET arrear = arrear - @Amount, {payColumn} = ISNULL({payColumn},0) + @Amount WHERE sn = @Sn",
                    new { Amount = amount, Sn = sn }, transaction);
            }
        }
        else if (typeVal == 1)
        {
            // 采购应付 → 更新 bill_buy
            var isReturn = await db.QueryFirstOrDefaultAsync<int?>(
                "SELECT CASE WHEN flag=2 OR total<0 THEN 1 ELSE 0 END FROM bill_buy WHERE sn = @Sn",
                new { Sn = sn }, transaction) == 1;
            if (isReturn)
            {
                await db.ExecuteAsync(
                    $"UPDATE bill_buy SET arrear = 0, {payColumn} = ISNULL({payColumn},0) + @Amount WHERE sn = @Sn",
                    new { Amount = amount, Sn = sn }, transaction);
            }
            else
            {
                await db.ExecuteAsync(
                    $"UPDATE bill_buy SET arrear = arrear - @Amount, {payColumn} = ISNULL({payColumn},0) + @Amount WHERE sn = @Sn",
                    new { Amount = amount, Sn = sn }, transaction);
            }
        }

        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 获取指定供应商指定年份的按月往来汇总
    /// 进货=bill_buy(supplier=sid)，total已含正负号直接SUM
    /// 出货=bill_sell(客户cid匹配)，total已含正负号直接SUM
    /// 若供应商在客户表中无匹配记录，则出货为0
    /// </summary>
    public async Task<IEnumerable<dynamic>> GetMonthlyTransactionSummaryAsync(string cid, int year)
    {
        using var db = await CreateConnectionAsync();
        var sql = $@"
            SELECT COALESCE(buy.month, sell.month) AS month,
                   ISNULL(buy.buy_total, 0) AS buy_total,
                   ISNULL(sell.sell_total, 0) AS sell_total,
                   ISNULL(buy.buy_settled, 0) AS buy_settled,
                   ISNULL(sell.sell_settled, 0) AS sell_settled
            FROM (
                SELECT MONTH(bb.datetime) AS month,
                       ISNULL(SUM(ISNULL(bb.total,0)), 0) AS buy_total,
                       ISNULL(SUM(ISNULL(bb.total,0) - ISNULL(bb.arrear,0)), 0) AS buy_settled
                FROM bill_buy bb
                WHERE YEAR(bb.datetime) = @Year
                AND ISNULL(bb.flag,0) IN ({(int)BusinessConstants.BillFlag.Confirmed}, {(int)BusinessConstants.BillFlag.Returned})
                AND bb.supplier = @Cid
                GROUP BY MONTH(bb.datetime)
            ) buy
            FULL OUTER JOIN (
                SELECT MONTH(bs.datetime) AS month,
                       ISNULL(SUM(ISNULL(bs.total,0)), 0) AS sell_total,
                       ISNULL(SUM(ISNULL(bs.total_payment,0)), 0) AS sell_settled
                FROM bill_sell bs
                WHERE YEAR(bs.datetime) = @Year
                AND ISNULL(bs.flag,0) IN ({(int)BusinessConstants.BillFlag.Confirmed}, {(int)BusinessConstants.BillFlag.Returned})
                AND bs.client IN (
                    SELECT c.cid FROM client_infor c
                    INNER JOIN supplier_infor s ON c.name LIKE s.name + '%'
                    WHERE s.sid = @Cid
                )
                GROUP BY MONTH(bs.datetime)
            ) sell ON buy.month = sell.month
            ORDER BY COALESCE(buy.month, sell.month)";
        return await db.QueryAsync<dynamic>(sql, new { Cid = cid, Year = year });
    }

    /// <summary>
    /// 获取指定年份有进货记录的供应商列表（含欠款合计）
    /// 以 bill_buy 当年有记录为准，欠款合计仍取 arrearage
    /// </summary>
    public async Task<IEnumerable<dynamic>> GetTransactionClientsAsync(int year, string? keyword = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = $@"SELECT s.sid, s.name
                    FROM supplier_infor s
                    INNER JOIN (
                        SELECT DISTINCT supplier FROM bill_buy
                        WHERE YEAR(datetime) = @Year AND ISNULL(flag,0) IN ({(int)BusinessConstants.BillFlag.Confirmed},{(int)BusinessConstants.BillFlag.Returned})
                    ) bb ON bb.supplier = s.sid";
        if (!string.IsNullOrEmpty(keyword))
            sql += " WHERE (s.name LIKE @Kw OR s.sid LIKE @Kw OR s.name_py LIKE @Kw)";
        sql += " ORDER BY s.name_py";
        return await db.QueryAsync<dynamic>(sql, new { Year = year, Kw = $"%{keyword}%" });
    }

    public async Task<int> DeleteBySnAsync(string sn, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.ExecuteAsync("DELETE FROM arrearage WHERE sn = @Sn", new { Sn = sn }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    /// <summary>
    /// 按类型和日期范围查询欠款列表（含退货取反和未付金额计算）
    /// 正常单：je=total, charge=charge, owe=total-charge
    /// 退货单：je=-total, charge=-charge, owe=-(total-charge)
    /// 负数单（total<0）：je=total, charge=charge, owe=total-charge（已带负号，不再取反）
    /// </summary>
    public async Task<IEnumerable<dynamic>> GetListWithCalcAsync(int? type = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"SELECT a.id, a.bid, a.sn,
                    CASE WHEN a.total < 0 THEN a.total
                         WHEN bs.flag=2 OR bs.total<0 THEN -a.total ELSE a.total END as je,
                    CASE WHEN a.total < 0 THEN ISNULL(a.charge,0)
                         WHEN bs.flag=2 OR bs.total<0 THEN -ISNULL(a.charge,0)
                         ELSE ISNULL(a.charge,0) END as charge,
                    CASE WHEN a.total < 0 THEN (ISNULL(a.total,0) - ISNULL(a.charge,0))
                         WHEN bs.flag=2 OR bs.total<0 THEN -(ISNULL(a.total,0) - ISNULL(a.charge,0))
                         ELSE (ISNULL(a.total,0) - ISNULL(a.charge,0))
                    END as owe,
                    a.[operator], a.type, a.btype, a.datetime,
                    CASE WHEN bs.flag=2 OR bs.total<0 THEN 1 ELSE 0 END as is_return
                    FROM arrearage a
                    LEFT JOIN bill_sell bs ON a.sn = bs.sn AND a.type = 2
                    LEFT JOIN bill_buy bb ON a.sn = bb.sn AND a.type = 1
                    WHERE 1=1";
        if (type.HasValue) sql += " AND a.type = @Type";
        if (startDate.HasValue) sql += " AND a.datetime >= @Start";
        if (endDate.HasValue) sql += " AND a.datetime < DATEADD(day, 1, @End)";
        sql += " ORDER BY a.datetime DESC";
        return await db.QueryAsync<dynamic>(sql, new { Type = type, Start = startDate, End = endDate });
    }

    // IRepository<Arrearage> 显式实现
    Task<Arrearage?> IRepository<Arrearage>.GetByIdAsync(object id) => throw new NotImplementedException();
    Task<IEnumerable<Arrearage>> IRepository<Arrearage>.GetAllAsync() => throw new NotImplementedException();
    Task<int> IRepository<Arrearage>.UpdateAsync(Arrearage entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<Arrearage>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<Arrearage>.CountAsync() => throw new NotImplementedException();
}
