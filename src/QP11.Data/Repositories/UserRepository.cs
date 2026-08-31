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

public class UserRepository : IUserRepository
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

    public async Task<IEnumerable<UserInfor>> GetAllAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<UserInfor>(
            "SELECT * FROM user_infor WHERE state = 1 ORDER BY username");
    }

    /// <summary>获取所有用户（含已禁用），用于用户管理界面</summary>
    public async Task<IEnumerable<UserInfor>> GetAllIncludingDisabledAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<UserInfor>(
            "SELECT * FROM user_infor ORDER BY username");
    }

    public async Task<UserInfor?> GetByIdAsync(string username)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<UserInfor>(
            "SELECT * FROM user_infor WHERE username = @Username", new { Username = username });
    }

    public async Task<int> InsertAsync(UserInfor entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO user_infor (username, password, name, [groups], state, auth)
                    VALUES (@Username, @Password, @Name, @Groups, @State, @Auth)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateAsync(UserInfor entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE user_infor SET name=@Name, [groups]=@Groups WHERE username=@Username";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdatePasswordAsync(string username, string newPassword)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE user_infor SET password = @Password WHERE username = @Username",
            new { Password = newPassword, Username = username });
    }

    public async Task<int> DisableAsync(string username)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE user_infor SET state = 0 WHERE username = @Username", new { Username = username });
    }

    public async Task<int> EnableAsync(string username)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE user_infor SET state = 1 WHERE username = @Username", new { Username = username });
    }

    // IRepository<UserInfor> 显式实现
    Task<UserInfor?> IRepository<UserInfor>.GetByIdAsync(object id) => GetByIdAsync(id.ToString()!);
    Task<int> IRepository<UserInfor>.InsertAsync(UserInfor entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<UserInfor>.UpdateAsync(UserInfor entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<UserInfor>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<UserInfor>.CountAsync() => throw new NotImplementedException();
}
