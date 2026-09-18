using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Entities;
using QP11.Data.Infrastructure;

namespace QP11.Services;

/// <summary>
/// 备忘录数据服务
/// 数据存储于 memo 表，按操作员（operator）隔离；
/// 所有数据访问前自动确保存储表存在（进程内仅建表一次，SQL Server 2000 兼容）
/// </summary>
public class MemoService
{
    private const string TableName = "memo";

    /// <summary>建表任务缓存，进程内只执行一次（并发安全）</summary>
    private static Task? _ensureTask;
    private static readonly object EnsureLock = new();

    /// <summary>创建并异步打开连接，避免 UI 线程同步阻塞</summary>
    protected async Task<DbConnection> CreateConnectionAsync()
    {
        var db = DatabaseFactory.Create();
        if (db.State != ConnectionState.Open)
            await db.OpenAsync();
        return db;
    }

    /// <summary>确保存储表存在。首次调用检测数据库并自动建表，之后直接复用已完成的 Task。</summary>
    public Task EnsureTableAsync()
    {
        if (_ensureTask != null) return _ensureTask;
        lock (EnsureLock)
        {
            _ensureTask ??= EnsureTableCoreAsync();
        }
        return _ensureTask;
    }

    private async Task EnsureTableCoreAsync()
    {
        try
        {
            using var db = await CreateConnectionAsync();
            var exists = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM sysobjects WHERE name = @TableName AND xtype = 'U'",
                new { TableName });
            if (exists > 0) return;

            await db.ExecuteAsync($@"CREATE TABLE {TableName} (
                id bigint IDENTITY(1,1) NOT NULL,
                [operator] varchar(50) NOT NULL,
                content nvarchar(500) NOT NULL,
                priority int NOT NULL CONSTRAINT DF_{TableName}_priority DEFAULT (1),
                done bit NOT NULL CONSTRAINT DF_{TableName}_done DEFAULT (0),
                remind_type int NOT NULL CONSTRAINT DF_{TableName}_remind_type DEFAULT (0),
                remind_at datetime NULL,
                countdown_minutes int NULL,
                reminded bit NOT NULL CONSTRAINT DF_{TableName}_reminded DEFAULT (0),
                create_time datetime NOT NULL CONSTRAINT DF_{TableName}_create_time DEFAULT (GETDATE()),
                done_time datetime NULL,
                CONSTRAINT PK_{TableName} PRIMARY KEY (id)
            )");
        }
        catch
        {
            // 建表失败时清除缓存，允许下次访问重试
            _ensureTask = null;
            throw;
        }
    }

    /// <summary>获取当前操作员的全部备忘（未完成在前，优先级高→低，提醒时间近→远）</summary>
    public async Task<List<Memo>> GetByOperatorAsync(string operatorName)
    {
        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();
        var rows = await db.QueryAsync<Memo>(
            $@"SELECT id, [operator], content, priority, done, remind_type, remind_at, countdown_minutes, reminded, create_time, done_time
               FROM {TableName}
               WHERE [operator] = @Operator
               ORDER BY done ASC, priority DESC, ISNULL(remind_at, '9999-12-31') ASC, create_time DESC",
            new { Operator = operatorName });
        return rows.ToList();
    }

    /// <summary>新增备忘条目</summary>
    public async Task<long> InsertAsync(Memo memo)
    {
        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();
        var sql = $@"INSERT INTO {TableName} ([operator], content, priority, done, remind_type, remind_at, countdown_minutes, reminded, create_time, done_time)
                     VALUES (@Operator, @Content, @Priority, @Done, @RemindType, @RemindAt, @CountdownMinutes, @Reminded, GETDATE(), @DoneTime);
                     SELECT CAST(SCOPE_IDENTITY() AS bigint)";
        return await db.ExecuteScalarAsync<long>(sql, memo);
    }

    /// <summary>更新备忘条目（内容/优先级/提醒/完成状态整体保存）</summary>
    public async Task<int> UpdateAsync(Memo memo)
    {
        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();
        var sql = $@"UPDATE {TableName} SET
                     content = @Content,
                     priority = @Priority,
                     done = @Done,
                     done_time = CASE WHEN @Done = 1 THEN ISNULL(done_time, GETDATE()) ELSE NULL END,
                     remind_type = @RemindType,
                     remind_at = @RemindAt,
                     countdown_minutes = @CountdownMinutes,
                     reminded = @Reminded
                     WHERE id = @Id";
        return await db.ExecuteAsync(sql, memo);
    }

    /// <summary>删除备忘条目</summary>
    public async Task<int> DeleteAsync(long id)
    {
        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync($"DELETE FROM {TableName} WHERE id = @Id", new { Id = id });
    }

    /// <summary>勾选/取消勾选完成状态</summary>
    public async Task<int> MarkDoneAsync(long id, bool done)
    {
        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync(
            $@"UPDATE {TableName} SET done = @Done, done_time = CASE WHEN @Done = 1 THEN GETDATE() ELSE NULL END WHERE id = @Id",
            new { Done = done ? 1 : 0, Id = id });
    }

    /// <summary>
    /// 抢占到期的未提醒条目（多机防重复）：事务内加行锁读取，
    /// 命中后立即置 reminded=1，返回本轮本机实际应提醒的条目。
    /// </summary>
    public async Task<List<Memo>> ClaimDueAsync(string operatorName)
    {
        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();
        using var txn = db.BeginTransaction();
        try
        {
            var due = (await db.QueryAsync<Memo>(
                $@"SELECT id, [operator], content, priority, done, remind_type, remind_at, countdown_minutes, reminded, create_time, done_time
                   FROM {TableName} (UPDLOCK)
                   WHERE [operator] = @Operator AND done = 0 AND remind_type > 0 AND reminded = 0
                     AND remind_at IS NOT NULL AND remind_at <= GETDATE()
                   ORDER BY remind_at",
                new { Operator = operatorName }, txn)).ToList();

            if (due.Count > 0)
            {
                var ids = due.Select(d => d.Id).ToList();
                await db.ExecuteAsync(
                    $"UPDATE {TableName} SET reminded = 1 WHERE id IN @Ids",
                    new { Ids = ids }, txn);
            }

            txn.Commit();
            return due;
        }
        catch
        {
            txn.Rollback();
            throw;
        }
    }
}