using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Entities;
using QP11.Data.Infrastructure;

namespace QP11.Data.Repositories;

public class ReminderRepository
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

    public async Task<IEnumerable<Reminder>> GetPendingAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<Reminder>(
            @"SELECT * FROM reminder WHERE status = '待处理' AND remind_date <= GETDATE() ORDER BY remind_date");
    }

    public async Task<IEnumerable<Reminder>> GetByTypeAsync(string type)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<Reminder>(
            "SELECT * FROM reminder WHERE type = @Type ORDER BY remind_date DESC", new { Type = type });
    }

    public async Task<int> InsertAsync(Reminder entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO reminder (type, target_id, target_name, content, remind_date, status, datetime, memo)
                    VALUES (@Type, @TargetId, @TargetName, @Content, @RemindDate, @Status, GETDATE(), @Memo)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateStatusAsync(long id, string status)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE reminder SET status = @Status WHERE id = @Id", new { Status = status, Id = id });
    }
}
