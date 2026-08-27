using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Interfaces;
using QP11.Data.Infrastructure;

namespace QP11.Data.Repositories;

public class BaseRepository<T> : IRepository<T> where T : class
{
    // 反射元数据缓存 — 避免每次CRUD操作重复反射
    private static readonly string _tableName = typeof(T).Name;
    private static readonly string _keyColumnName = CacheKeyColumnName();
    private static readonly string _selectByIdSql = $"SELECT * FROM {_tableName} WHERE {_keyColumnName} = @Id";
    private static readonly string _selectAllSql = $"SELECT * FROM {_tableName}";
    private static readonly string _countSql = $"SELECT COUNT(*) FROM {_tableName}";
    private static readonly string _deleteByIdSql = $"DELETE FROM {_tableName} WHERE {_keyColumnName} = @Id";

    // Insert/Update SQL按属性列表缓存
    private static readonly (string columns, string paramNames)? _insertSql = CacheInsertSql();
    private static readonly (string setClause, string keyColumn)? _updateSql = CacheUpdateSql();

    private static string CacheKeyColumnName()
    {
        var keyProp = typeof(T).GetProperties()
            .FirstOrDefault(p => p.GetCustomAttributes(false)
                .Any(a => a.GetType().Name == "KeyAttribute"));
        return keyProp?.Name ?? "Id";
    }

    private static (string columns, string paramNames)? CacheInsertSql()
    {
        var properties = typeof(T).GetProperties()
            .Where(p => p.Name != _keyColumnName || !IsIdentityKey(p)).ToList();
        if (properties.Count == 0) return null;
        return (string.Join(", ", properties.Select(p => p.Name)),
                string.Join(", ", properties.Select(p => $"@{p.Name}")));
    }

    private static (string setClause, string keyColumn)? CacheUpdateSql()
    {
        var properties = typeof(T).GetProperties()
            .Where(p => p.Name != _keyColumnName).ToList();
        if (properties.Count == 0) return null;
        return (string.Join(", ", properties.Select(p => $"{p.Name} = @{p.Name}")),
                _keyColumnName);
    }

    private static bool IsIdentityKey(PropertyInfo prop)
    {
        var dbGenerated = prop.GetCustomAttribute<DatabaseGeneratedAttribute>();
        return dbGenerated != null && dbGenerated.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity;
    }

    protected DbConnection CreateConnection() => DatabaseFactory.Create();

    /// <summary>
    /// 创建并异步打开数据库连接。
    /// DatabaseFactory.Create() 返回未打开连接，此处调用 OpenAsync() 在线程池执行 Open，
    /// 避免在 UI 线程上同步阻塞（远程 SQL Server 2000 单次 Open 可达 1-3 秒）。
    /// </summary>
    protected async Task<DbConnection> CreateConnectionAsync()
    {
        var db = DatabaseFactory.Create();
        if (db.State != ConnectionState.Open)
        {
            await db.OpenAsync();
        }
        return db;
    }

    public virtual async Task<T?> GetByIdAsync(object id)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<T>(_selectByIdSql, new { Id = id });
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<T>(_selectAllSql);
    }

    public virtual async Task<int> InsertAsync(T entity, IDbTransaction? transaction = null)
    {
        var ownsConnection = transaction == null;
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        try
        {
            if (_insertSql == null) return 0;
            var (columns, paramNames) = _insertSql.Value;
            var sql = $"INSERT INTO {_tableName} ({columns}) VALUES ({paramNames})";
            return await db.ExecuteAsync(sql, entity, transaction);
        }
        finally
        {
            if (ownsConnection) db.Dispose();
        }
    }

    public virtual async Task<int> UpdateAsync(T entity, IDbTransaction? transaction = null)
    {
        var ownsConnection = transaction == null;
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        try
        {
            if (_updateSql == null) return 0;
            var (setClause, keyColumn) = _updateSql.Value;
            var sql = $"UPDATE {_tableName} SET {setClause} WHERE {keyColumn} = @{keyColumn}";
            return await db.ExecuteAsync(sql, entity, transaction);
        }
        finally
        {
            if (ownsConnection) db.Dispose();
        }
    }

    public virtual async Task<int> DeleteAsync(object id, IDbTransaction? transaction = null)
    {
        var ownsConnection = transaction == null;
        var db = transaction?.Connection ?? await CreateConnectionAsync();
        try
        {
            return await db.ExecuteAsync(_deleteByIdSql, new { Id = id }, transaction);
        }
        finally
        {
            if (ownsConnection) db.Dispose();
        }
    }

    public virtual async Task<int> CountAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteScalarAsync<int>(_countSql);
    }
}
