using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Entities;
using QP11.Data.Infrastructure;

namespace QP11.Data.Repositories;

public class ShippingOrderRepository
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

    public async Task<ShippingOrder?> GetBySnAsync(string sn)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<ShippingOrder>(
            "SELECT * FROM shipping_order WHERE sn = @Sn", new { Sn = sn });
    }

    public async Task<IEnumerable<ShippingOrder>> GetListAsync(DateTime? startDate = null, DateTime? endDate = null, string? client = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = "SELECT * FROM shipping_order WHERE ISNULL(flag, 0) <> -1";
        if (startDate.HasValue) sql += " AND datetime >= @Start";
        if (endDate.HasValue) sql += " AND datetime < DATEADD(day, 1, @End)";
        if (!string.IsNullOrEmpty(client)) sql += " AND client = @Client";
        sql += " ORDER BY datetime DESC";
        return await db.QueryAsync<ShippingOrder>(sql, new { Start = startDate, End = endDate, Client = client });
    }

    public async Task<int> InsertAsync(ShippingOrder entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO shipping_order (sn, sell_sn, client, address, logistics, logistics_no, worker, flag, datetime, memo)
                    VALUES (@Sn, @SellSn, @Client, @Address, @Logistics, @LogisticsNo, @Worker, @Flag, GETDATE(), @Memo)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateStatusAsync(string sn, int flag)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE shipping_order SET flag=@Flag WHERE sn=@Sn", new { Flag = flag, Sn = sn });
    }

    public async Task<int> UpdateLogisticsNoAsync(string sn, string logisticsNo)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE shipping_order SET logistics_no=@LogisticsNo WHERE sn=@Sn", new { LogisticsNo = logisticsNo, Sn = sn });
    }
}
