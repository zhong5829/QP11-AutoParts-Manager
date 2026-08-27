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

public class PaysRepository : IPaysRepository
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

    public async Task<IEnumerable<Pays>> GetByAccountAsync(long accountId, DateTime? startDate = null, DateTime? endDate = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = "SELECT * FROM pays WHERE bid = @Bid";
        if (startDate.HasValue) sql += " AND datetime >= @Start";
        if (endDate.HasValue) sql += " AND datetime < DATEADD(day, 1, @End)";
        sql += " ORDER BY datetime DESC";
        return await db.QueryAsync<Pays>(sql, new { Bid = accountId.ToString(), Start = startDate, End = endDate });
    }

    // pays 表实际列: id, bid, sn, pay, operator, flag, btype, bz, datetime
    public async Task<int> InsertAsync(Pays entity, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO pays (sn, [operator], pay, datetime, flag, btype, bz)
                    VALUES (@Sn, @Worker, @Je, GETDATE(), @Flag, @Btype, @Bz)";
        var result = await db.ExecuteAsync(sql, entity, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    // IRepository<Pays> 显式实现
    Task<Pays?> IRepository<Pays>.GetByIdAsync(object id) => throw new NotImplementedException();
    Task<IEnumerable<Pays>> IRepository<Pays>.GetAllAsync() => throw new NotImplementedException();
    Task<int> IRepository<Pays>.UpdateAsync(Pays entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<Pays>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<Pays>.CountAsync() => throw new NotImplementedException();
}
