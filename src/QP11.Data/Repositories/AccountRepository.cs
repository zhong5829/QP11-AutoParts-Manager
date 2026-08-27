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

public class AccountRepository : IAccountRepository
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

    public async Task<IEnumerable<Account>> GetAllAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<Account>("SELECT * FROM account WHERE (flag IS NULL OR flag = 0) ORDER BY id");
    }

    public async Task<Account?> GetByIdAsync(long id)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<Account>("SELECT * FROM account WHERE id = @Id", new { Id = id });
    }

    public async Task<int> InsertAsync(Account entity, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO account (name, type, charge, flag, memo) VALUES (@Name, @Type, @Je, 0, @Memo)";
        var result = await db.ExecuteAsync(sql, entity, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<int> UpdateAsync(Account entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE account SET name=@Name, type=@Type, charge=@Je, memo=@Memo WHERE id=@Id";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateBalanceAsync(long id, decimal amount, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.ExecuteAsync("UPDATE account SET charge = ISNULL(charge,0) + @Amount WHERE id = @Id",
            new { Amount = amount, Id = id }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<IEnumerable<dynamic>> GetIncomeExpenseListAsync(DateTime? startDate = null, DateTime? endDate = null, int? flag = null)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"SELECT id, name, sn, charge, flag, type, [operator], memo, datetime, bz,
                    CASE flag WHEN 1 THEN charge ELSE 0 END as income,
                    CASE flag WHEN 0 THEN charge ELSE 0 END as expense
                    FROM account WHERE (flag IS NULL OR flag >= 0)";
        if (flag.HasValue) sql += " AND flag = @Flag";
        if (startDate.HasValue) sql += " AND datetime >= @Start";
        if (endDate.HasValue) sql += " AND datetime < DATEADD(day, 1, @End)";
        sql += " ORDER BY datetime DESC, sn DESC";
        return await db.QueryAsync<dynamic>(sql, new { Flag = flag, Start = startDate, End = endDate });
    }

    // IRepository<Account> 显式实现
    Task<Account?> IRepository<Account>.GetByIdAsync(object id) => GetByIdAsync(Convert.ToInt64(id));
    Task<int> IRepository<Account>.UpdateAsync(Account entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<Account>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<Account>.CountAsync() => throw new NotImplementedException();
}
