using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Threading;
using System.Threading.Tasks;

namespace QP11.Data.Infrastructure;

public class OdbcCompatConnection : DbConnection
{
    private readonly OdbcConnection _inner;
    private ConnectionState _state = ConnectionState.Closed;
    /// <summary>记录当前活跃事务的内部 OdbcTransaction（用于命令执行前绑定）</summary>
    private OdbcTransaction? _activeInnerTransaction;

    public OdbcCompatConnection(string connectionString)
    {
        _inner = new OdbcConnection(connectionString);
        _inner.StateChange += (s, e) => _state = e.CurrentState;
    }

    public OdbcConnection InnerConnection => _inner;

    public override string ConnectionString
    {
        get => _inner.ConnectionString;
#pragma warning disable CS8765
        set => _inner.ConnectionString = value;
#pragma warning restore CS8765
    }

    public override string Database => _inner.Database;
    public override ConnectionState State => _state;
    public override string DataSource => _inner.DataSource;
    public override string ServerVersion => _inner.ServerVersion;
    public override int ConnectionTimeout => _inner.ConnectionTimeout;

    public override void Open() { _inner.Open(); _state = _inner.State; }
    public override void Close() { _inner.Close(); _state = _inner.State; }
    public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        var innerTxn = _inner.BeginTransaction(isolationLevel);
        _activeInnerTransaction = innerTxn;
        return new OdbcCompatTransaction(innerTxn, this);
    }

    /// <summary>获取当前连接上活跃的内部 OdbcTransaction（可能为 null）</summary>
    internal OdbcTransaction? ActiveInnerTransaction => _activeInnerTransaction;

    /// <summary>事务结束时清除引用（由 OdbcCompatTransaction 调用）</summary>
    internal void OnTransactionDisposed()
    {
        _activeInnerTransaction = null;
    }

    protected override DbCommand CreateDbCommand()
    {
        return new OdbcCompatCommand(_inner.CreateCommand(), this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}

public class OdbcCompatCommand : DbCommand
{
    private readonly OdbcCommand _inner;
    private readonly OdbcCompatConnection _connection;
    private string _originalSql = "";
    private OdbcCompatParameterCollection? _paramWrapper;
    /// <summary>缓存事务引用（ODBC 驱动可能重置 _inner.Transaction，getter 优先返回此值）</summary>
    private DbTransaction? _cachedTransaction;

    public OdbcCompatCommand(OdbcCommand inner, OdbcCompatConnection connection)
    {
        _inner = inner;
        _connection = connection;
    }

    public override string CommandText
    {
        get => _originalSql;
#pragma warning disable CS8765
        set => _originalSql = value;
#pragma warning restore CS8765
    }

    public override int CommandTimeout
    {
        get => _inner.CommandTimeout;
        set => _inner.CommandTimeout = value;
    }

    public override CommandType CommandType
    {
        get => _inner.CommandType;
        set => _inner.CommandType = value;
    }

    protected override DbConnection? DbConnection
    {
        get => _connection;
        set { }
    }

    protected override DbParameterCollection DbParameterCollection =>
        _paramWrapper ??= new OdbcCompatParameterCollection(_inner.Parameters);

    public override UpdateRowSource UpdatedRowSource
    {
        get => _inner.UpdatedRowSource;
        set => _inner.UpdatedRowSource = value;
    }

    public override bool DesignTimeVisible
    {
        get => _inner.DesignTimeVisible;
        set => _inner.DesignTimeVisible = value;
    }

    protected override DbTransaction? DbTransaction
    {
        get => _cachedTransaction ?? _inner.Transaction;
        set
        {
            _cachedTransaction = value;
            _inner.Transaction = value is OdbcCompatTransaction compat ? compat.InnerTransaction : (OdbcTransaction?)value;
        }
    }

    public override void Cancel() => _inner.Cancel();

    /// <summary>执行前确保内部命令绑定了事务（ODBC 驱动可能重置 _inner.Transaction）</summary>
    private void EnsureInnerTransaction()
    {
        if (_inner.Transaction != null) return;

        if (_cachedTransaction != null)
        {
            _inner.Transaction = _cachedTransaction is OdbcCompatTransaction compat
                ? compat.InnerTransaction
                : (OdbcTransaction)_cachedTransaction;
            return;
        }

        var connTxn = _connection.ActiveInnerTransaction;
        if (connTxn != null)
            _inner.Transaction = connTxn;
    }

    public override int ExecuteNonQuery() { PrepareCommand(); EnsureInnerTransaction(); return _inner.ExecuteNonQuery(); }
    public override object? ExecuteScalar() { PrepareCommand(); EnsureInnerTransaction(); return _inner.ExecuteScalar(); }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        PrepareCommand();
        EnsureInnerTransaction();
        return _inner.ExecuteReader(behavior);
    }

    protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

    public override void Prepare() => _inner.Prepare();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>将 @命名参数 SQL 转换为 ?位置参数 SQL，委托给共享的 SqlParamConverter</summary>
    private void PrepareCommand() => SqlParamConverter.ApplyToParameters(_originalSql, _inner);
}

public class OdbcCompatParameterCollection : DbParameterCollection
{
    private readonly OdbcParameterCollection _inner;

    public OdbcCompatParameterCollection(OdbcParameterCollection inner)
    {
        _inner = inner;
    }

    public override int Count => _inner.Count;
    public override object SyncRoot => _inner.SyncRoot;
    public override bool IsFixedSize => _inner.IsFixedSize;
    public override bool IsReadOnly => _inner.IsReadOnly;
    public override bool IsSynchronized => _inner.IsSynchronized;

    public override int Add(object value) => _inner.Add(value);
    public override void AddRange(Array values) => _inner.AddRange(values);
    public override void Clear() => _inner.Clear();
    public override bool Contains(object value) => _inner.Contains(value);
    public override bool Contains(string value) => _inner.Contains(value);
    public override void CopyTo(Array array, int index) => _inner.CopyTo(array, index);
    public override IEnumerator GetEnumerator() => _inner.GetEnumerator();
    public override int IndexOf(object value) => _inner.IndexOf(value);
    public override int IndexOf(string parameterName) => _inner.IndexOf(parameterName);
    public override void Insert(int index, object value) => _inner.Insert(index, value);
    public override void Remove(object value) => _inner.Remove(value);
    public override void RemoveAt(int index) => _inner.RemoveAt(index);
    public override void RemoveAt(string parameterName) => _inner.RemoveAt(parameterName);

    protected override DbParameter GetParameter(int index) => _inner[index];
    protected override DbParameter GetParameter(string parameterName) => _inner[parameterName];
    protected override void SetParameter(int index, DbParameter value) => _inner[index] = (OdbcParameter)value;
    protected override void SetParameter(string parameterName, DbParameter value) => _inner[parameterName] = (OdbcParameter)value;
}

/// <summary>
/// 包装 OdbcTransaction，确保 .Connection 返回 OdbcCompatConnection 而非内部原生连接，
/// 使 Dapper 通过 transaction.Connection 创建命令时能走 OdbcCompatCommand 的参数转换。
/// </summary>
public class OdbcCompatTransaction : DbTransaction
{
    private readonly OdbcTransaction _inner;
    private readonly OdbcCompatConnection _ownerConnection;

    public OdbcCompatTransaction(OdbcTransaction inner, OdbcCompatConnection ownerConnection)
    {
        _inner = inner;
        _ownerConnection = ownerConnection;
    }

    public OdbcTransaction InnerTransaction => _inner;

    protected override DbConnection DbConnection => _ownerConnection;

    public override IsolationLevel IsolationLevel => _inner.IsolationLevel;

    public override void Commit() => _inner.Commit();
    public override void Rollback() => _inner.Rollback();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _ownerConnection.OnTransactionDisposed();
        }
        base.Dispose(disposing);
    }
}
