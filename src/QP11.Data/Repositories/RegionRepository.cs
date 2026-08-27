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

public class RegionRepository : IRegionRepository
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

    public async Task<IEnumerable<Region>> GetChildrenAsync(long? parentId)
    {
        using var db = await CreateConnectionAsync();
        if (parentId.HasValue)
            return await db.QueryAsync<Region>(
                "SELECT * FROM region WHERE parent_id = @Pid ORDER BY sort", new { Pid = parentId });
        return await db.QueryAsync<Region>(
            "SELECT * FROM region WHERE parent_id IS NULL ORDER BY sort");
    }

    public async Task<int> InsertAsync(Region entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO region (parent_id, name, code, sort)
                    VALUES (@ParentId, @Name, @Code, @Sort)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateAsync(Region entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE region SET parent_id=@ParentId, name=@Name, code=@Code, sort=@Sort WHERE id=@Id";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> DeleteAsync(long id)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM region WHERE id = @Id", new { Id = id });
    }
}
