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
/// 单据打印状态服务
/// 数据存储于 print_log 表：只记录「已打印」的单据，表中无记录即视为未打印，
/// 因此不需要对历史单据做全量回填。
/// 所有数据访问前自动确保存储表存在（进程内仅建表一次，SQL Server 2000 兼容）
/// </summary>
public class PrintLogService
{
    private const string TableName = "print_log";
    private const string IndexName = "UX_print_log_sn";

    /// <summary>销售单类型标识（与 BillPrintData.BillType 一致）</summary>
    public const string BillTypeSell = "销售";

    /// <summary>退货单类型标识（与 BillPrintData.BillType 一致）</summary>
    public const string BillTypeReturn = "退货";

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

            var tableExists = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM sysobjects WHERE name = @TableName AND xtype = 'U'",
                new { TableName });
            if (tableExists == 0)
            {
                await db.ExecuteAsync($@"CREATE TABLE {TableName} (
                    id bigint IDENTITY(1,1) NOT NULL,
                    bill_type varchar(10) NOT NULL,
                    sn varchar(15) NOT NULL,
                    printed bit NOT NULL CONSTRAINT DF_{TableName}_printed DEFAULT (0),
                    print_count int NOT NULL CONSTRAINT DF_{TableName}_print_count DEFAULT (0),
                    first_print_time datetime NULL,
                    last_print_time datetime NULL,
                    last_operator varchar(20) NULL,
                    CONSTRAINT PK_{TableName} PRIMARY KEY (id)
                )");
            }

            // SQL Server 2000 无 CREATE INDEX IF NOT EXISTS，先查 sysindexes 再建
            var indexExists = await db.ExecuteScalarAsync<int>(
                $"SELECT COUNT(1) FROM sysindexes WHERE id = OBJECT_ID('{TableName}') AND name = @IndexName",
                new { IndexName });
            if (indexExists == 0)
            {
                await db.ExecuteAsync($"CREATE UNIQUE INDEX {IndexName} ON {TableName}(sn)");
            }
        }
        catch
        {
            // 建表失败时清除缓存，允许下次访问重试
            _ensureTask = null;
            throw;
        }
    }

    /// <summary>
    /// 标记单据已打印（仅在打印成功后调用），重复打印会累加打印次数。
    /// 事务内加行锁读取：已存在则更新，不存在则插入（与备忘录抢占同款写法）。
    /// 按 sn 定位而非 (bill_type, sn)：bill_sell.sn 全局唯一，且各打印入口传入的
    /// bill_type 未必一致（销售开单页查询模式对退货单也传「销售」），按 sn 定位可避免同一单出现两条记录
    /// </summary>
    public async Task MarkPrintedAsync(string? billType, string? sn, string? operatorName)
    {
        if (string.IsNullOrWhiteSpace(billType) || string.IsNullOrWhiteSpace(sn)) return;

        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();
        using var txn = db.BeginTransaction();
        try
        {
            var id = await db.ExecuteScalarAsync<long?>(
                $"SELECT id FROM {TableName} WITH (UPDLOCK) WHERE sn = @Sn",
                new { Sn = sn }, txn);

            if (id.HasValue)
            {
                await db.ExecuteAsync(
                    $@"UPDATE {TableName} SET bill_type = @BillType, printed = 1, print_count = print_count + 1,
                       last_print_time = GETDATE(), last_operator = @Operator
                       WHERE id = @Id",
                    new { BillType = billType, Operator = operatorName, Id = id.Value }, txn);
            }
            else
            {
                await db.ExecuteAsync(
                    $@"INSERT INTO {TableName} (bill_type, sn, printed, print_count, first_print_time, last_print_time, last_operator)
                       VALUES (@BillType, @Sn, 1, 1, GETDATE(), GETDATE(), @Operator)",
                    new { BillType = billType, Sn = sn, Operator = operatorName }, txn);
            }

            txn.Commit();
        }
        catch
        {
            txn.Rollback();
            throw;
        }
    }

    /// <summary>取消打印标记（人工补正）：删除记录，使其重新计入未打印</summary>
    public async Task UnmarkPrintedAsync(string? sn)
    {
        if (string.IsNullOrWhiteSpace(sn)) return;

        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();
        await db.ExecuteAsync($"DELETE FROM {TableName} WHERE sn = @Sn", new { Sn = sn });
    }

    /// <summary>批量查询指定单号中已打印的单号集合（用于列表展示打印状态）</summary>
    public async Task<HashSet<string>> GetPrintedSnsAsync(IEnumerable<string>? sns)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = sns?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (list == null || list.Count == 0) return result;

        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();
        var rows = await db.QueryAsync<string>(
            $"SELECT sn FROM {TableName} WHERE printed = 1 AND sn IN @Sns",
            new { Sns = list });
        foreach (var s in rows)
            if (!string.IsNullOrEmpty(s)) result.Add(s);
        return result;
    }

    /// <summary>查询当天创建且未打印的销售单/退货单（全店范围，不限操作员）</summary>
    public async Task<List<UnprintedBill>> GetUnprintedTodayAsync()
    {
        await EnsureTableAsync();
        using var db = await CreateConnectionAsync();

        // bill_sell.sn 为主键（全局唯一），故只按 sn 关联即可，无需在 ON 中重复判断单据类型
        var rows = (await db.QueryAsync<UnprintedBill>(
            $@"SELECT b.sn AS Sn,
                      CASE WHEN b.flag = 2 THEN '{BillTypeReturn}' ELSE '{BillTypeSell}' END AS BillType,
                      b.datetime AS Datetime,
                      b.client AS Client,
                      b.[operator] AS BillOperator,
                      ISNULL(b.total, 0) AS Total
               FROM bill_sell b
               LEFT JOIN {TableName} p ON p.sn = b.sn AND p.printed = 1
               WHERE b.datetime >= @Today AND b.datetime < @Tomorrow
                 AND b.flag IN (1, 2)
                 AND p.sn IS NULL
               ORDER BY b.datetime DESC",
            new { Today = DateTime.Today, Tomorrow = DateTime.Today.AddDays(1) })).ToList();

        if (rows.Count == 0) return rows;

        // 客户名单独查询填充，避免在 bill_sell 大表上做额外 join
        var cids = rows.Select(r => r.Client).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        if (cids.Count > 0)
        {
            var clients = (await db.QueryAsync<ClientInfor>(
                "SELECT cid, name FROM client_infor WHERE cid IN @Cids", new { Cids = cids })).ToList();
            var map = clients.Where(c => c.Cid != null)
                             .ToDictionary(c => c.Cid!, c => c.Name ?? "", StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows)
                r.ClientName = r.Client != null && map.TryGetValue(r.Client, out var n) ? n : "";
        }

        return rows;
    }
}

/// <summary>未打印单据（提醒弹窗与列表展示用）</summary>
public class UnprintedBill
{
    public string? Sn { get; set; }
    public string? BillType { get; set; }
    public DateTime? Datetime { get; set; }
    public string? Client { get; set; }
    /// <summary>客户名称，单独查询填充</summary>
    public string? ClientName { get; set; }
    public string? BillOperator { get; set; }
    public decimal? Total { get; set; }
}