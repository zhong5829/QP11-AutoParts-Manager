using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using QP11.Data.Infrastructure;

namespace QP11.Services;

/// <summary>
/// 往来账手动修改值的数据库持久化服务
/// 数据存储于 transaction_override 表，键为 供应商sid_年份_月份
/// 所有数据访问前自动确保存储表存在（进程内仅建表一次，SQL Server 2000 兼容）
/// </summary>
public class TransactionOverrideService
{
    private const string TableName = "transaction_override";

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

    /// <summary>
    /// 确保存储表存在。首次调用检测数据库并自动建表，之后直接复用已完成的 Task。
    /// </summary>
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
                sid varchar(30) NOT NULL,
                [year] int NOT NULL,
                [month] int NOT NULL,
                buy_total decimal(18,2) NOT NULL CONSTRAINT DF_{TableName}_buy DEFAULT (0),
                sell_total decimal(18,2) NOT NULL CONSTRAINT DF_{TableName}_sell DEFAULT (0),
                is_settled bit NOT NULL CONSTRAINT DF_{TableName}_settled DEFAULT (0),
                CONSTRAINT PK_{TableName} PRIMARY KEY (sid, [year], [month])
            )");
        }
        catch
        {
            // 建表失败时清除缓存，允许下次访问重试
            _ensureTask = null;
            throw;
        }
    }

    /// <summary>获取覆盖值，无则返回null</summary>
    public async Task<OverrideEntry?> GetOverrideAsync(string sid, int year, int month)
    {
        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<OverrideEntry>(
            $"SELECT sid, [year], [month], buy_total, sell_total, is_settled FROM {TableName} WHERE sid = @Sid AND [year] = @Year AND [month] = @Month",
            new { Sid = sid, Year = year, Month = month });
    }

    /// <summary>保存覆盖值（无则插入，有则更新）</summary>
    public async Task SaveOverrideAsync(string sid, int year, int month, decimal buyTotal, decimal sellTotal, bool isSettled)
    {
        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();
        var updated = await db.ExecuteAsync(
            $"UPDATE {TableName} SET buy_total = @Buy, sell_total = @Sell, is_settled = @Settled WHERE sid = @Sid AND [year] = @Year AND [month] = @Month",
            new { Sid = sid, Year = year, Month = month, Buy = buyTotal, Sell = sellTotal, Settled = isSettled ? 1 : 0 });
        if (updated == 0)
        {
            await db.ExecuteAsync(
                $"INSERT INTO {TableName} (sid, [year], [month], buy_total, sell_total, is_settled) VALUES (@Sid, @Year, @Month, @Buy, @Sell, @Settled)",
                new { Sid = sid, Year = year, Month = month, Buy = buyTotal, Sell = sellTotal, Settled = isSettled ? 1 : 0 });
        }
    }

    /// <summary>删除覆盖值（恢复为数据库原值）</summary>
    public async Task RemoveOverrideAsync(string sid, int year, int month)
    {
        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();
        await db.ExecuteAsync(
            $"DELETE FROM {TableName} WHERE sid = @Sid AND [year] = @Year AND [month] = @Month",
            new { Sid = sid, Year = year, Month = month });
    }
}

public class OverrideEntry
{
    public string? sid { get; set; }
    public int year { get; set; }
    public int month { get; set; }
    public decimal buy_total { get; set; }
    public decimal sell_total { get; set; }
    public bool is_settled { get; set; }
}
