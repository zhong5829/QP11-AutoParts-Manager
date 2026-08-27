using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Entities;
using QP11.Data.Infrastructure;

namespace QP11.Data.Repositories;

public class PartImageRepository
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

    public async Task<IEnumerable<PartImage>> GetByPartIdAsync(long partid)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<PartImage>(
            "SELECT * FROM part_image WHERE partid = @Partid ORDER BY sort", new { Partid = partid });
    }

    public async Task<int> InsertAsync(PartImage entity)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync(
            "INSERT INTO part_image (partid, image_path, sort, memo) VALUES (@Partid, @ImagePath, @Sort, @Memo)", entity);
    }

    public async Task<int> DeleteAsync(long id)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM part_image WHERE id = @Id", new { Id = id });
    }
}
