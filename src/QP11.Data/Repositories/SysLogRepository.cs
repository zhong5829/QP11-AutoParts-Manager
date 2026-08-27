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

public class SysLogRepository : ISysLogRepository
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

    public async Task<(IEnumerable<SysLog> Data, int Total)> GetListAsync(int page = 1, int pageSize = 50, string? keyword = null, DateTime? startDate = null, DateTime? endDate = null, string? operatorName = null, string? action = null)
    {
        using var db = await CreateConnectionAsync();
        var where = "WHERE 1=1";
        if (startDate.HasValue) where += " AND datetime >= @Start";
        if (endDate.HasValue) where += " AND datetime < DATEADD(day, 1, @End)";
        if (!string.IsNullOrEmpty(operatorName)) where += " AND operator = @OperatorName";
        if (!string.IsNullOrEmpty(action)) where += " AND action LIKE @Action";
        if (!string.IsNullOrEmpty(keyword)) where += " AND (operator LIKE @Kw OR module LIKE @Kw OR action LIKE @Kw)";

        var countSql = $"SELECT COUNT(*) FROM sys_log {where}";
        var total = await db.ExecuteScalarAsync<int>(countSql,
            new { Start = startDate, End = endDate, OperatorName = operatorName, Action = $"%{action}%", Kw = $"%{keyword}%" });

        var offset = (page - 1) * pageSize;
        var dataSql = $@"
            SELECT TOP {pageSize} * FROM sys_log {where}
            AND id NOT IN (SELECT TOP {offset} id FROM sys_log {where} ORDER BY datetime DESC)
            ORDER BY datetime DESC";
        var data = await db.QueryAsync<SysLog>(dataSql,
            new { Start = startDate, End = endDate, OperatorName = operatorName, Action = $"%{action}%", Kw = $"%{keyword}%" });

        return (data, total);
    }

    public async Task<int> InsertAsync(SysLog log)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO sys_log (operator, module, action, datetime)
                    VALUES (@Operator, @Module, @Action, GETDATE())";
        return await db.ExecuteAsync(sql, log);
    }

    public async Task<int> DeleteBeforeAsync(DateTime date)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM sys_log WHERE datetime < @Date", new { Date = date });
    }

    // IRepository<SysLog> 显式实现
    Task<SysLog?> IRepository<SysLog>.GetByIdAsync(object id) => throw new NotImplementedException();
    Task<IEnumerable<SysLog>> IRepository<SysLog>.GetAllAsync() => throw new NotImplementedException();
    Task<int> IRepository<SysLog>.InsertAsync(SysLog entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<SysLog>.UpdateAsync(SysLog entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<SysLog>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<SysLog>.CountAsync() => throw new NotImplementedException();
}
