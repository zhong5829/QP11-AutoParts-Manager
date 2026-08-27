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

public class MemberCardRepository : IMemberCardRepository
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

    public async Task<IEnumerable<MemberCard>> GetAllAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<MemberCard>(
            "SELECT * FROM xl_hygl WHERE (zt IS NULL OR zt <> '停用') ORDER BY kh");
    }

    public async Task<MemberCard?> GetByIdAsync(string kh)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<MemberCard>(
            "SELECT * FROM xl_hygl WHERE kh = @Kh", new { Kh = kh });
    }

    public async Task<IEnumerable<MemberCard>> SearchAsync(string keyword)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<MemberCard>(
            @"SELECT * FROM xl_hygl WHERE (zt IS NULL OR zt <> '停用')
              AND (kh LIKE @Kw OR khmc LIKE @Kw OR tel LIKE @Kw) ORDER BY kh",
            new { Kw = $"%{keyword}%" });
    }

    public async Task<int> InsertAsync(MemberCard entity, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var sql = @"INSERT INTO xl_hygl (kh, klb, kmm, khmc, lxr, tel, cp, carname, cartype, je, zkl, zt, ykcs)
                    VALUES (@Kh, @Klb, @Kmm, @Khmc, @Lxr, @Tel, @Cp, @Carname, @Cartype, @Je, @Zkl, @Zt, @Ykcs)";
        var result = await db.ExecuteAsync(sql, entity, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<int> UpdateAsync(MemberCard entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE xl_hygl SET khmc=@Khmc, lxr=@Lxr, tel=@Tel, je=@Je, zkl=@Zkl, zt=@Zt
                    WHERE kh=@Kh";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> RechargeAsync(string kh, decimal amount, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.ExecuteAsync(
            "UPDATE xl_hygl SET je = ISNULL(je,0) + @Amount WHERE kh = @Kh",
            new { Amount = amount, Kh = kh }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<int> ConsumeAsync(string kh, decimal amount, IDbTransaction? transaction = null)
    {
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        var result = await db.ExecuteAsync(
            "UPDATE xl_hygl SET je = ISNULL(je,0) - @Amount, ykcs = ISNULL(ykcs,0) + 1 WHERE kh = @Kh AND ISNULL(je,0) >= @Amount",
            new { Amount = amount, Kh = kh }, transaction);
        if (transaction == null) db.Dispose();
        return result;
    }

    public async Task<int> LogicDeleteAsync(string kh)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE xl_hygl SET zt='停用' WHERE kh=@Kh", new { Kh = kh });
    }

    // IRepository<MemberCard> 显式实现
    Task<MemberCard?> IRepository<MemberCard>.GetByIdAsync(object id) => GetByIdAsync(id.ToString()!);
    Task<int> IRepository<MemberCard>.UpdateAsync(MemberCard entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<MemberCard>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<MemberCard>.CountAsync() => throw new NotImplementedException();
}
