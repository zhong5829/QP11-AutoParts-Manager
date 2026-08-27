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

public class BorrowRepository : IBorrowRepository
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

    public async Task<IEnumerable<Borrow>> GetAllAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<Borrow>(
            "SELECT * FROM xl_gjgl ORDER BY jyrq DESC");
    }

    public async Task<IEnumerable<Borrow>> GetByStatusAsync(string status)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<Borrow>(
            "SELECT * FROM xl_gjgl WHERE zt = @Status ORDER BY jyrq DESC",
            new { Status = status });
    }

    public async Task<int> InsertAsync(Borrow entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO xl_gjgl (gjbh, gjmc, bz, jybz, jyr, jyrq, zt, gjjz, gjmc_py, jybz_py, jyr_py)
                    VALUES (@Gjbh, @Gjmc, @Bz, @Jybz, @Jyr, GETDATE(), @Zt, @Gjjz, @GjmcPy, @JybzPy, @JyrPy)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateStatusAsync(long id, string status)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync(
            "UPDATE xl_gjgl SET zt = @Status, ghrq = GETDATE() WHERE id = @Id",
            new { Status = status, Id = id });
    }

    // IRepository<Borrow> 显式实现
    Task<Borrow?> IRepository<Borrow>.GetByIdAsync(object id) => throw new NotImplementedException();
    Task<int> IRepository<Borrow>.InsertAsync(Borrow entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<Borrow>.UpdateAsync(Borrow entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<Borrow>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<Borrow>.CountAsync() => throw new NotImplementedException();
}
