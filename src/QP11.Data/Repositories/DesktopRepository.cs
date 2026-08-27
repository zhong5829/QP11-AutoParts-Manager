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

public class DesktopRepository : IDesktopRepository
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

    public async Task<IEnumerable<Desktop>> GetByUsernameAsync(string username)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<Desktop>(
            "SELECT * FROM desktop WHERE username = @Username ORDER BY code",
            new { Username = username });
    }

    public async Task<int> InsertAsync(Desktop entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO desktop (code, name, buildtime, memo, username)
                    VALUES (@Code, @Name, GETDATE(), @Memo, @Username)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> DeleteAsync(string code, string username)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync(
            "DELETE FROM desktop WHERE code = @Code AND username = @Username",
            new { Code = code, Username = username });
    }
}
