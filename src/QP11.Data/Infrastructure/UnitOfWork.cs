using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using QP11.Core.Interfaces;
using QP11.Data.Infrastructure;

namespace QP11.Data.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnectionFactory? _dbFactory;
    private DbConnection? _connection;
    private IDbTransaction? _transaction;
    private bool _disposed;

    public UnitOfWork(IDbConnectionFactory? dbFactory = null)
    {
        _dbFactory = dbFactory;
    }

    public IDbTransaction Transaction => _transaction ?? throw new InvalidOperationException("事务未启动。请先调用 BeginTransactionAsync。");

    /// <summary>返回包装后的连接（ODBC模式下为 OdbcCompatConnection，确保参数转换生效）</summary>
    public IDbConnection Connection => _connection ?? throw new InvalidOperationException("连接未创建。请先调用 BeginTransactionAsync。");

    public async Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        // DatabaseFactory.Create() 返回未打开连接，需先异步打开再开启事务
        _connection = _dbFactory != null ? _dbFactory.Create() : DatabaseFactory.Create();
        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync();
        _transaction = _connection.BeginTransaction(isolationLevel);
    }

    public async Task CommitAsync()
    {
        try
        {
            _transaction?.Commit();
        }
        catch
        {
            _transaction?.Rollback();
            throw;
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
        await Task.CompletedTask;
    }

    public async Task RollbackAsync()
    {
        _transaction?.Rollback();
        _transaction?.Dispose();
        _transaction = null;
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _transaction?.Dispose();
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
