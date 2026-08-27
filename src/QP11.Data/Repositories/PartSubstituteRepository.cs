using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Entities;
using QP11.Data.Infrastructure;

namespace QP11.Data.Repositories;

public class PartSubstituteRepository
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

    public async Task<IEnumerable<PartSubstitute>> GetByPartIdAsync(long partid)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<PartSubstitute>(
            "SELECT * FROM part_substitute WHERE partid = @Partid", new { Partid = partid });
    }

    public async Task<int> InsertAsync(PartSubstitute entity)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync(
            "INSERT INTO part_substitute (partid, sub_partid, memo) VALUES (@Partid, @SubPartid, @Memo)", entity);
    }

    public async Task<int> DeleteAsync(long id)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM part_substitute WHERE id = @Id", new { Id = id });
    }
}
