using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Entities;
using QP11.Data.Infrastructure;

namespace QP11.Data.Repositories;

public class QuotationRepository
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

    public async Task<Quotation?> GetBySnAsync(string sn)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<Quotation>(
            "SELECT * FROM quotation WHERE sn = @Sn", new { Sn = sn });
    }

    public async Task<IEnumerable<Quotation>> GetListAsync(DateTime? startDate = null, DateTime? endDate = null, string? client = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"SELECT * FROM quotation WHERE ISNULL(flag, 0) <> -1";
        if (startDate.HasValue) sql += " AND datetime >= @Start";
        if (endDate.HasValue) sql += " AND datetime < DATEADD(day, 1, @End)";
        if (!string.IsNullOrEmpty(client)) sql += " AND client = @Client";
        sql += " ORDER BY datetime DESC";
        return await db.QueryAsync<Quotation>(sql, new { Start = startDate, End = endDate, Client = client });
    }

    public async Task<IEnumerable<QuotationDetail>> GetDetailsAsync(string sn)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<QuotationDetail>(
            "SELECT * FROM quotation_detail WHERE sn = @Sn", new { Sn = sn });
    }

    public async Task<int> InsertBillAsync(Quotation bill)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO quotation (sn, client, worker, total, flag, datetime, memo)
                    VALUES (@Sn, @Client, @Worker, @Total, @Flag, GETDATE(), @Memo)";
        return await db.ExecuteAsync(sql, bill);
    }

    public async Task<int> InsertDetailAsync(QuotationDetail detail)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO quotation_detail (sn, partid, amount, price, stotal, memo)
                    VALUES (@Sn, @Partid, @Amount, @Price, @Stotal, @Memo)";
        return await db.ExecuteAsync(sql, detail);
    }

    public async Task<int> UpdateStatusAsync(string sn, int flag)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE quotation SET flag=@Flag WHERE sn=@Sn", new { Flag = flag, Sn = sn });
    }

    public async Task<int> LogicDeleteAsync(string sn)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE quotation SET flag = -1 WHERE sn=@Sn", new { Sn = sn });
    }
}
