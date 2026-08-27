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

public class PartBatchRepository : IPartBatchRepository
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

    public async Task<IEnumerable<PartBatch>> GetByPartIdAsync(long partid)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<PartBatch>(
            "SELECT * FROM part_batch WHERE partid = @Partid ORDER BY datetime DESC",
            new { Partid = partid });
    }

    public async Task<IEnumerable<PartBatch>> GetExpiringAsync(int days = 30)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<PartBatch>(
            @"SELECT * FROM part_batch
              WHERE expire_date BETWEEN GETDATE() AND DATEADD(day, @Days, GETDATE())
              AND remain > 0 ORDER BY expire_date",
            new { Days = days });
    }

    public async Task<int> InsertAsync(PartBatch entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO part_batch (partid, batch_no, supplier, amount, remain, inprice, produce_date, expire_date, datetime, memo)
                    VALUES (@Partid, @BatchNo, @Supplier, @Amount, @Remain, @Inprice, @ProduceDate, @ExpireDate, GETDATE(), @Memo)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateRemainAsync(long id, decimal remain)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE part_batch SET remain = @Remain WHERE id = @Id", new { Remain = remain, Id = id });
    }

    public async Task<int> LogicDeleteAsync(long id)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM part_batch WHERE id = @Id", new { Id = id });
    }
}
